using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ManagedWinapi.Windows;

namespace ManagedWinapi.Audio.Mixer
{
    /// <summary>
    /// Represents a mixer provided by a sound card. Each mixer has
    /// multiple destination lines (e. g. Record and Playback) of which
    /// each has multiple source lines (Wave, MIDI, Mic, etc.).
    /// </summary>
    public class Mixer : IDisposable
    {
        private MIXERCAPS mc;
        private IList<DestinationLine> destLines;

        /// <summary>
        /// Occurs when a control of this mixer changes value.
        /// </summary>
        public MixerEventHandler ControlChanged;

        /// <summary>
        /// Occurs when a line of this mixer changes.
        /// </summary>
        public MixerEventHandler LineChanged;

        private Mixer(IntPtr hMixer)
        {
            Handle = hMixer;
            EventDispatchingNativeWindow.Instance.EventHandler += ednw_EventHandler;
            mixerGetDevCapsA(hMixer, ref mc, Marshal.SizeOf(mc));
        }

        private void ednw_EventHandler(ref System.Windows.Forms.Message m, ref bool handled)
        {
            if (!CreateEvents) return;
            if (m.Msg == MM_MIXM_CONTROL_CHANGE && m.WParam == Handle)
            {
                int ctrlID = m.LParam.ToInt32();
                MixerControl c = FindControl(ctrlID);
                if (c == null) return;
                ControlChanged?.Invoke(this, new MixerEventArgs(this, c.Line, c));
                c.OnChanged();
            }
            else if (m.Msg == MM_MIXM_LINE_CHANGE && m.WParam == Handle)
            {
                int lineID = m.LParam.ToInt32();
                MixerLine l = FindLine(lineID);
                if (l == null) return;
                if (ControlChanged != null)
                {
                    LineChanged(this, new MixerEventArgs(this, l, null));
                }
                l.OnChanged();
            }
        }

        /// <summary>
        /// Whether to create change events.
        /// Enabling this may create a slight performance impact, so only
        /// enable it if you handle these events.
        /// </summary>
        public bool CreateEvents { get; set; }

        internal IntPtr Handle { get; private set; }

        /// <summary>
        /// Gets the name of this mixer's sound card.
        /// </summary>
        public string Name => mc.szPname;

        /// <summary>
        /// Gets the number of destination lines of this mixer.
        /// </summary>
        public int DestinationLineCount => mc.cDestinations;

        /// <summary>
        /// Gets all destination lines of this mixer
        /// </summary>
        public IList<DestinationLine> DestinationLines
        {
            get
            {
                if (destLines != null) return destLines;
                int dlc = DestinationLineCount;
                List<DestinationLine> l = new(dlc);
                for (int i = 0; i < dlc; i++)
                {
                    l.Add(DestinationLine.GetLine(this, i));
                }
                destLines = l.AsReadOnly();
                return destLines;
            }
        }

        /// <summary>
        /// Disposes this mixer.
        /// </summary>
        public void Dispose()
        {
            if (destLines != null)
            {
                foreach (DestinationLine dl in destLines)
                {
                    dl.Dispose();
                }
                destLines = null;
            }

            if (Handle.ToInt32() == 0) return;
            mixerClose(Handle);
            EventDispatchingNativeWindow.Instance.EventHandler -= ednw_EventHandler;
            Handle = IntPtr.Zero;
        }

        /// <summary>
        /// Find a line of this mixer by ID.
        /// </summary>
        /// <param name="lineId">ID of the line to find</param>
        /// <returns>The line, or <code>null</code> if no line was found.</returns>
        private MixerLine FindLine(int lineId)
        {
            return DestinationLines.Select(dl => dl.findLine(lineId)).FirstOrDefault(found => found != null);
        }

        /// <summary>
        /// Find a control of this mixer by ID.
        /// </summary>
        /// <param name="ctrlId">ID of the control to find.</param>
        /// <returns>The control, or <code>null</code> if no control was found.</returns>
        private MixerControl FindControl(int ctrlId)
        {
            return DestinationLines.Select(dl => dl.findControl(ctrlId)).FirstOrDefault(found => found != null);
        }

        #region PInvoke Declarations

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern uint mixerGetNumDevs();

        [DllImport("winmm.dll")]
        private static extern Int32 mixerOpen(ref IntPtr phmx, uint pMxId,
           IntPtr dwCallback, IntPtr dwInstance, UInt32 fdwOpen);

        [DllImport("winmm.dll")]
        private static extern Int32 mixerClose(IntPtr hmx);

        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        private static extern int mixerGetDevCapsA(IntPtr uMxId, ref MIXERCAPS
        pmxcaps, int cbmxcaps);

        private struct MIXERCAPS
        {
            public short wMid;
            public short wPid;
            public int vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public int fdwSupport;
            public int cDestinations;
        }
        private static readonly uint CALLBACK_WINDOW = 0x00010000;
        private static readonly int MM_MIXM_LINE_CHANGE = 0x3D0;
        private static readonly int MM_MIXM_CONTROL_CHANGE = 0x3D1;
        #endregion
    }

    /// <summary>
    /// Represents the method that will handle the <b>LineChanged</b> or 
    /// <b>ControlChanged</b> event of a <see cref="Mixer">Mixer</see>.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">A <see cref="MixerEventArgs">MixerEventArgs</see> 
    /// that contains the event data.</param>
    public delegate void MixerEventHandler(object sender, MixerEventArgs e);

    /// <summary>
    /// Provides data for the LineChanged and ControlChanged events of a 
    /// <see cref="Mixer">Mixer</see>.
    /// </summary>
    public class MixerEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the 
        /// <see cref="MixerEventArgs">MixerEventArgs</see> class.
        /// </summary>
        /// <param name="mixer">The affected mixer</param>
        /// <param name="line">The affected line</param>
        /// <param name="control">The affected control, or <code>null</code>
        /// if this is a LineChanged event.</param>
        public MixerEventArgs(Mixer mixer, MixerLine line, MixerControl control)
        {
            Mixer = mixer;
            Line = line;
            Control = control;
        }

        /// <summary>
        /// The affected mixer.
        /// </summary>
        public Mixer Mixer { get; }

        /// <summary>
        /// The affected line.
        /// </summary>
        public MixerLine Line { get; }

        /// <summary>
        /// The affected control.
        /// </summary>
        public MixerControl Control { get; }
    }
}