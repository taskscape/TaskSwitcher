/*
 * ManagedWinapi - A collection of .NET components that wrap PInvoke calls to 
 * access native API by managed code. http://mwinapi.sourceforge.net/
 * Copyright (C) 2006 Michael Schierl
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2.1 of the License, or (at your option) any later version.
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library; see the file COPYING. if not, visit
 * http://www.gnu.org/licenses/lgpl.html or write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace ManagedWinapi
{
    /// <summary>
    /// A chunk in another processes memory. Mostly used to allocate buffers
    /// in another process for sending messages to its windows.
    /// </summary>
    public class ProcessMemoryChunk : IDisposable
    {
        readonly Process process;
        readonly IntPtr location, hProcess;
        readonly int size;
        readonly bool free;

        /// <summary>
        /// Create a new memory chunk that points to existing memory.
        /// Mostly used to read that memory.
        /// </summary>
        public ProcessMemoryChunk(Process process, IntPtr location, int size)
        {
            this.process = process;
            this.hProcess = OpenProcess(ProcessAccessFlags.VMOperation | ProcessAccessFlags.VMRead | ProcessAccessFlags.VMWrite, false, process.Id);
            ApiHelper.FailIfZero(hProcess);
            this.location = location;
            this.size = size;
            this.free = false;
        }

        private ProcessMemoryChunk(Process process, IntPtr hProcess, IntPtr location, int size, bool free)
        {
            this.process = process;
            this.hProcess = hProcess;
            this.location = location;
            this.size = size;
            this.free = free;
        }

        /// <summary>
        /// The process this chunk refers to.
        /// </summary>
        public Process Process => process;

        /// <summary>
        /// The location in memory (of the other process) this chunk refers to.
        /// </summary>
        public IntPtr Location => location;

        /// <summary>
        /// The size of the chunk.
        /// </summary>
        public int Size => size;

        /// <summary>
        /// Allocate a chunk in another process.
        /// </summary>
        public static ProcessMemoryChunk Alloc(Process process, int size)
        {
            IntPtr hProcess = OpenProcess(ProcessAccessFlags.VMOperation | ProcessAccessFlags.VMRead | ProcessAccessFlags.VMWrite, false, process.Id);
            IntPtr remotePointer = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)size,
                MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            ApiHelper.FailIfZero(remotePointer);
            return new ProcessMemoryChunk(process, hProcess, remotePointer, size, true);
        }

        /// <summary>
        /// Allocate a chunk in another process and unmarshal a struct
        /// there.
        /// </summary>
        public static ProcessMemoryChunk AllocStruct(Process process, object structure)
        {
            int size = Marshal.SizeOf(structure);
            ProcessMemoryChunk result = Alloc(process, size);
            result.WriteStructure(0, structure);
            return result;
        }

        /// <summary>
        /// Free the memory in the other process, if it has been allocated before.
        /// </summary>
        public void Dispose()
        {
            if (free)
            {
                if (!VirtualFreeEx(hProcess, location, UIntPtr.Zero, MEM_RELEASE))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            CloseHandle(hProcess);
        }

        /// <summary>
        /// Write a structure into this chunk.
        /// </summary>
        public void WriteStructure(int offset, object structure)
        {
            int structureSize = Marshal.SizeOf(structure);
            IntPtr localPtr = Marshal.AllocHGlobal(structureSize);
            try
            {
                Marshal.StructureToPtr(structure, localPtr, false);
                Write(offset, localPtr, structureSize);
            }
            finally
            {
                Marshal.FreeHGlobal(localPtr);
            }
        }

        /// <summary>
        /// Write into this chunk.
        /// </summary>
        public void Write(int offset, IntPtr ptr, int length)
        {
            if (offset < 0) throw new ArgumentException("Offset may not be negative", "offset");
            if (offset + length > size) throw new ArgumentException("Exceeding chunk size");
            WriteProcessMemory(hProcess, new IntPtr(location.ToInt64() + offset), ptr, new UIntPtr((uint)length), IntPtr.Zero);
        }

        /// <summary>
        /// Write a byte array into this chunk.
        /// </summary>
        public void Write(int offset, byte[] ptr)
        {
            if (offset < 0) throw new ArgumentException("Offset may not be negative", "offset");
            if (offset + ptr.Length > size) throw new ArgumentException("Exceeding chunk size");
            WriteProcessMemory(hProcess, new IntPtr(location.ToInt64() + offset), ptr, new UIntPtr((uint)ptr.Length), IntPtr.Zero);
        }

        /// <summary>
        /// Read this chunk.
        /// </summary>
        /// <returns></returns>
        public byte[] Read() { return Read(0, size); }

        /// <summary>
        /// Read a part of this chunk.
        /// </summary>
        public byte[] Read(int offset, int length)
        {
            if (offset + length > size) throw new ArgumentException("Exceeding chunk size");
            byte[] result = new byte[length];
            ReadProcessMemory(hProcess, new IntPtr(location.ToInt64() + offset), result, new UIntPtr((uint)length), IntPtr.Zero);
            return result;
        }

        /// <summary>
        /// Read this chunk to a pointer in this process.
        /// </summary>
        public void ReadToPtr(IntPtr ptr)
        {
            ReadToPtr(0, size, ptr);
        }

        /// <summary>
        /// Read a part of this chunk to a pointer in this process.
        /// </summary>
        public void ReadToPtr(int offset, int length, IntPtr ptr)
        {
            if (offset + length > size) throw new ArgumentException("Exceeding chunk size");
            ReadProcessMemory(hProcess, new IntPtr(location.ToInt64() + offset), ptr, new UIntPtr((uint)length), IntPtr.Zero);
        }

        /// <summary>
        /// Read a part of this chunk to a structure.
        /// </summary>
        public object ReadToStructure(int offset, Type structureType)
        {
            int structureSize = Marshal.SizeOf(structureType);
            IntPtr localPtr = Marshal.AllocHGlobal(structureSize);
            try
            {
                ReadToPtr(offset, structureSize, localPtr);
                return Marshal.PtrToStructure(localPtr, structureType);
            }
            finally
            {
                Marshal.FreeHGlobal(localPtr);
            }
        }

        #region PInvoke Declarations

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress,
            uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, bool bInheritHandle,
           int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int CloseHandle(IntPtr hObject);

        private static readonly uint MEM_COMMIT = 0x1000, MEM_RESERVE = 0x2000,
            MEM_RELEASE = 0x8000, PAGE_READWRITE = 0x04;

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress,
           UIntPtr dwSize, uint dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
           [Out] byte[] lpBuffer, UIntPtr nSize, IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
           IntPtr lpBuffer, UIntPtr nSize, IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
           byte[] lpBuffer, UIntPtr nSize, IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
           IntPtr lpBuffer, UIntPtr nSize, IntPtr lpNumberOfBytesWritten);

        #endregion
    }

    internal enum ProcessAccessFlags : int
    {
        All = 0x001F0FFF,
        Terminate = 0x00000001,
        CreateThread = 0x00000002,
        VMOperation = 0x00000008,
        VMRead = 0x00000010,
        VMWrite = 0x00000020,
        DupHandle = 0x00000040,
        SetInformation = 0x00000200,
        QueryInformation = 0x00000400,
        Synchronize = 0x00100000
    }
}
