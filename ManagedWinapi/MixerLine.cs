using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace ManagedWinapi.Audio.Mixer
{
    /// <summary>
    /// Represents a mixer line, either a source line or a destination line.
    /// </summary>
    public abstract class MixerLine : IDisposable
    {
        /// <summary>
        /// Occurs when this line changes.
        /// </summary>
        public EventHandler Changed;

        internal MIXERLINE line;
        internal Mixer mixer;
        private MixerControl[] controls;

        internal MixerLine(Mixer mixer, MIXERLINE line)
        {
            this.mixer = mixer;
            this.line = line;
        }

        ///
        public virtual void Dispose()
        {
        }

        /// <summary>
        /// All controls of this line.
        /// </summary>
        private MixerControl[] Controls => controls ??= MixerControl.GetControls(mixer, this, ControlCount);

/*
        /// <summary>
        /// The volume control of this line, if it has one.
        /// </summary>
        public FaderMixerControl VolumeControl
        {
            get
            {
                foreach (MixerControl c in Controls)
                {
                    if (c.ControlType == MixerControlType.MIXERCONTROL_CONTROLTYPE_VOLUME)
                    {
                        return (FaderMixerControl)c;
                    }
                }
                return null;
            }
        }
*/

/*
        /// <summary>
        /// The mute switch of this control, if it has one.
        /// </summary>
        public BooleanMixerControl MuteSwitch
        {
            get
            {
                foreach (MixerControl c in Controls)
                {
                    if (c.ControlType == MixerControlType.MIXERCONTROL_CONTROLTYPE_MUTE)
                    {
                        return (BooleanMixerControl)c;
                    }
                }
                return null;

            }
        }
*/

        /// <summary>
        /// Gets the ID of this line.
        /// </summary>
        public int Id => line.dwLineID;

        /// <summary>
        /// Gets the number of channels of this line.
        /// </summary>
        public int ChannelCount => line.cChannels;

        /// <summary>
        /// Gets the number of controls of this line.
        /// </summary>
        private int ControlCount => line.cControls;

/*
        /// <summary>
        /// Gets the short name of this line;
        /// </summary>
        public string ShortName => line.szShortName;
*/

/*
        /// <summary>
        /// Gets the full name of this line.
        /// </summary>
        public string Name => line.szName;
*/

/*
        /// <summary>
        /// Gets the component type of this line;
        /// </summary>
        public MixerLineComponentType ComponentType => (MixerLineComponentType)line.dwComponentType;
*/

        /// <summary>
        /// The mixer that owns this line.
        /// </summary>
        public Mixer Mixer => mixer;

        internal MixerLine findLine(int lineId)
        {
            if (Id == lineId) { return this; }

            return ChildLines.Select(ml => ml.findLine(lineId)).FirstOrDefault(found => found != null);
        }

        private static readonly IList<MixerLine> EMPTY_LIST =
            new List<MixerLine>().AsReadOnly();

        protected virtual IList<MixerLine> ChildLines => EMPTY_LIST;

        internal MixerControl findControl(int ctrlId)
        {
            foreach (MixerControl c in Controls)
            {
                if (c.Id == ctrlId) return c;
            }

            return ChildLines.Select(l => l.findControl(ctrlId)).FirstOrDefault(found => found != null);
        }

        internal void OnChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        #region PInvoke Declarations

        internal struct MIXERLINE
        {
            public int cbStruct;
            public int dwDestination;
            public int dwSource;
            public int dwLineID;
            public int fdwLine;
            public int dwUser;
            public int dwComponentType;
            public int cChannels;
            public int cConnections;
            public int cControls;
            [MarshalAs(UnmanagedType.ByValTStr,
            SizeConst = 16)]
            public string szShortName;
            [MarshalAs(UnmanagedType.ByValTStr,
            SizeConst = 64)]
            public string szName;
            public int dwType;
            public int dwDeviceID;
            public int wMid;
            public int wPid;
            public int vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
        }

        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        internal static extern int mixerGetLineInfoA(IntPtr hmxobj, ref 
            MIXERLINE pmxl, int fdwInfo);

        internal static int MIXER_GETLINEINFOF_DESTINATION = 0;
        internal static int MIXER_GETLINEINFOF_SOURCE = 1;

        #endregion
    }

    /// <summary>
    /// Represents a destination line. There is one destination line for
    /// each way sound can leave the mixer. Usually there are two destination lines,
    /// one for playback and one for recording.
    /// </summary>
    public class DestinationLine : MixerLine
    {
        private DestinationLine(Mixer mixer, MIXERLINE line) : base(mixer, line) { }

        /// <summary>
        /// Gets the number of source lines of this destination line.
        /// </summary>
        private int SourceLineCount => line.cConnections;

        private IList<SourceLine> srcLines;

        /// <summary>
        /// Gets all source lines of this destination line.
        /// </summary>
        private IList<SourceLine> SourceLines
        {
            get
            {
                if (srcLines != null) return srcLines;
                List<SourceLine> sls = new(SourceLineCount);
                for (int i = 0; i < SourceLineCount; i++)
                {
                    sls.Add(SourceLine.GetLine(mixer, line.dwDestination, i));
                }
                srcLines = sls.AsReadOnly();
                return srcLines;
            }
        }

        internal static DestinationLine GetLine(Mixer mixer, int index)
        {
            MIXERLINE m = new();
            m.cbStruct = Marshal.SizeOf(m);
            m.dwDestination = index;
            mixerGetLineInfoA(mixer.Handle, ref m, MIXER_GETLINEINFOF_DESTINATION);
            return new DestinationLine(mixer, m);
        }

        ///
        public override void Dispose()
        {
        }

        private IList<MixerLine> childLines;

        protected override IList<MixerLine> ChildLines
        {
            get
            {
                if (childLines != null)
                {
                    return childLines;
                }

                List<MixerLine> cl = SourceLines.Cast<MixerLine>().ToList();
                childLines = cl.AsReadOnly();
                return childLines;
            }
        }
    }

    /// <summary>
    /// Represents a source line. Source lines represent way sound for one
    /// destination enters the mixer. So, if you can both record and playback
    /// CD audio, there will be two CD audio source lines, one for the Recording
    /// destination line and one for the Playback destination line.
    /// </summary>
    public class SourceLine : MixerLine
    {
        private SourceLine(Mixer m, MIXERLINE l) : base(m, l) { }

        internal static SourceLine GetLine(Mixer mixer, int destIndex, int srcIndex)
        {
            MIXERLINE m = new();
            m.cbStruct = Marshal.SizeOf(m);
            m.dwDestination = destIndex;
            m.dwSource = srcIndex;
            mixerGetLineInfoA(mixer.Handle, ref m, MIXER_GETLINEINFOF_SOURCE);
            return new SourceLine(mixer, m);
        }
    }
}
