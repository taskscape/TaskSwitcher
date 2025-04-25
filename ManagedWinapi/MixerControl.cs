using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ManagedWinapi.Audio.Mixer
{
    /// <summary>
    /// A control of a mixer line. This can be for example a volume slider
    /// or a mute switch.
    /// </summary>
    public class MixerControl
    {
        /// <summary>
        /// Occurs when the value of this control is changed
        /// </summary>
        public EventHandler Changed;

        internal MIXERCONTROL ctrl;
        internal Mixer mx;
        internal MixerLine ml;

        internal MixerControl(Mixer mx, MixerLine ml, MIXERCONTROL ctrl)
        {
            this.mx = mx;
            this.ml = ml;
            this.ctrl = ctrl;
        }

        /// <summary>
        /// The ID of this control.
        /// </summary>
        public int Id => ctrl.dwControlID;

        /// <summary>
        /// The class of this control. For example FADER or SWITCH.
        /// </summary>
        public MixerControlClass Class => (MixerControlClass) (ctrl.dwControlType & MIXERCONTROL_CT_CLASS_MASK);

        /// <summary>
        /// The type of the control. For example mute switch.
        /// </summary>
        public MixerControlType ControlType => (MixerControlType) ctrl.dwControlType;

        /// <summary>
        /// The flags of this control.
        /// </summary>
        public MixerControlFlags Flags => (MixerControlFlags) ctrl.fdwControl;

        /// <summary>
        /// Whether this control is uniform. A uniform control controls
        /// more than one channel, but can only set one value for all
        /// channels.
        /// </summary>
        public bool IsUniform => (Flags & MixerControlFlags.UNIFORM) != 0;

        /// <summary>
        /// Whether this control has multiple values per channel. An
        /// example for a multiple value control is a three-band equalizer.
        /// </summary>
        public bool IsMultiple => (Flags & MixerControlFlags.MULTIPLE) != 0;

        /// <summary>
        /// The number of channels.
        /// </summary>
        public int ChannelCount => ml.ChannelCount;

        /// <summary>
        /// The number of multiple values. For a three band equalizer,
        /// this is 3. Will be always one if IsMultiple is false.
        /// </summary>
        public int MultipleValuesCount => IsMultiple ? ctrl.cMultipleItems : 1;

        /// <summary>
        /// The line this control belongs to.
        /// </summary>
        public MixerLine Line => ml;

        /// <summary>
        /// The mixer this control belongs to.
        /// </summary>
        public Mixer Mixer => mx;

        internal static MixerControl[] GetControls(Mixer mx, MixerLine line, int controlCount)
        {
            if (controlCount == 0) return new MixerControl[0];
            MIXERCONTROL[] mc = new MIXERCONTROL[controlCount];
            int mxsize = Marshal.SizeOf(mc[0]);
            if (mxsize != 148) throw new Exception("" + mxsize);
            //mxsize = 148;

            MIXERLINECONTROLS mlc = new();
            mlc.cbStruct = Marshal.SizeOf(mlc);
            mlc.cControls = controlCount;
            mlc.dwLineID = line.Id;

            mlc.pamxctrl = Marshal.AllocCoTaskMem(mxsize * controlCount);
            mlc.cbmxctrl = mxsize;

            int err;
            if ((err = mixerGetLineControlsA(mx.Handle, ref mlc, MIXER_GETLINECONTROLSF_ALL)) != 0)
            {
                throw new Win32Exception("Error #" + err + " calling mixerGetLineControls()\n");
            }

            for (int i = 0; i < controlCount; i++)
            {
                mc[i] = (MIXERCONTROL) Marshal.PtrToStructure(new IntPtr(mlc.pamxctrl.ToInt64() + mxsize * i),
                    typeof(MIXERCONTROL))!;
            }

            Marshal.FreeCoTaskMem(mlc.pamxctrl);
            MixerControl[] result = new MixerControl[controlCount];
            for (int i = 0; i < controlCount; i++)
            {
                result[i] = GetControl(mx, line, mc[i]);
            }

            return result;
        }

        private static MixerControl GetControl(Mixer mx, MixerLine ml, MIXERCONTROL mc)
        {
            MixerControl result = new(mx, ml, mc);
            switch (result.Class)
            {
                case MixerControlClass.FADER when ((uint) result.ControlType & MIXERCONTROL_CT_UNITS_MASK) ==
                                                  (uint) MixerControlType.MIXERCONTROL_CT_UNITS_UNSIGNED:
                    result = new FaderMixerControl(mx, ml, mc);
                    break;
                case MixerControlClass.SWITCH when
                    ((uint) result.ControlType & MIXERCONTROL_CT_SUBCLASS_MASK) ==
                    (uint) MixerControlType.MIXERCONTROL_CT_SC_SWITCH_BOOLEAN &&
                    ((uint) result.ControlType & MIXERCONTROL_CT_UNITS_MASK) ==
                    (uint) MixerControlType.MIXERCONTROL_CT_UNITS_BOOLEAN:
                    result = new BooleanMixerControl(mx, ml, mc);
                    break;
            }

            return result;
        }

        internal void OnChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        #region PInvoke Declarations

        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        private static extern int mixerGetLineControlsA(IntPtr hmxobj, ref
            MIXERLINECONTROLS pmxlc, int fdwControls);

        private struct MIXERLINECONTROLS
        {
            public int cbStruct;
            public int dwLineID;

            public int dwControl;
            public int cControls;
            public int cbmxctrl;
            public IntPtr pamxctrl;
        }

#pragma warning disable 649
        internal struct MIXERCONTROL
        {
            public int cbStruct;
            public int dwControlID;
            public uint dwControlType;
            public int fdwControl;
            public int cMultipleItems;

            [MarshalAs(UnmanagedType.ByValTStr,
                SizeConst = 16)]
            public string szShortName;

            [MarshalAs(UnmanagedType.ByValTStr,
                SizeConst = 64)]
            public string szName;

            public int lMinimum;
            public int lMaximum;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10, ArraySubType = UnmanagedType.I4)]
            public int[] reserved;
        }
#pragma warning restore 649

        internal struct MIXERCONTROLDETAILS
        {
            public int cbStruct;
            public int dwControlID;
            public int cChannels;
            public int cMultipleItems;
            public int cbDetails;
            public IntPtr paDetails;
        }

        internal struct MIXERCONTROLDETAILS_UNSIGNED
        {
            public int dwValue;
        }

        internal struct MIXERCONTROLDETAILS_BOOLEAN
        {
            public int fValue;
        }

        private static readonly int MIXER_GETLINECONTROLSF_ALL = 0x0;
        private static readonly uint MIXERCONTROL_CT_CLASS_MASK = 0xF0000000;

        private static readonly uint MIXERCONTROL_CT_SUBCLASS_MASK = 0x0F000000;
        private static readonly uint MIXERCONTROL_CT_UNITS_MASK = 0x00FF0000;

        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        internal static extern int mixerGetControlDetailsA(IntPtr hmxobj, ref
            MIXERCONTROLDETAILS pmxcd, int fdwDetails);

        [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
        internal static extern int mixerSetControlDetails(IntPtr hmxobj, ref
            MIXERCONTROLDETAILS pmxcd, int fdwDetails);

        #endregion

    }

    /// <summary>
    /// A mixer control that is adjusted by a vertical fader, with a linear scale 
    /// of positive values (ie, 0 is the lowest possible value).
    /// </summary>
    public class FaderMixerControl : MixerControl
    {
        internal FaderMixerControl(Mixer mx, MixerLine ml, MIXERCONTROL mc) : base(mx, ml, mc)
        {
        }
    }

    /// <summary>
    /// A control that is has only two states (ie, values), 
    /// and is therefore adjusted via a button.
    /// </summary>
    public class BooleanMixerControl : MixerControl
    {
        internal BooleanMixerControl(Mixer mx, MixerLine ml, MIXERCONTROL mc) : base(mx, ml, mc)
        {
        }
    }

    /// <summary>
    /// Mixer control type classes. These classes are roughly based upon what type of 
    /// value a control adjusts, and therefore what kind of graphical user interface 
    /// you would normally present to the enduser to let him adjust that control's value. 
    /// The descriptions for these classes have been taken from 
    /// http://www.borg.com/~jglatt/tech/mixer.htm.
    /// </summary>
    public enum MixerControlClass
    {
        /// <summary>
        /// 	A custom class of control. If none of the others are applicable.
        /// </summary>
        CUSTOM = 0x00000000,

        /// <summary>
        /// A control that is adjusted by a graphical meter.
        /// </summary>
        METER = 0x10000000,

        /// <summary>
        /// A control that is has only two states (ie, values), and is 
        /// therefore adjusted via a button. 
        /// </summary>
        SWITCH = 0x20000000,

        /// <summary>
        /// A control that is adjusted by numeric entry.
        /// </summary>
        NUMBER = 0x30000000,

        /// <summary>
        /// A control that is adjusted by a horizontal slider 
        /// with a linear scale of negative and positive values. 
        /// (ie, Generally, 0 is the mid or "neutral" point).
        /// </summary>
        SLIDER = 0x40000000,

        /// <summary>
        /// A control that is adjusted by a vertical fader, with 
        /// a linear scale of positive values (ie, 0 is the lowest 
        /// possible value).
        /// </summary>
        FADER = 0x50000000,

        /// <summary>
        /// A control that allows the user to enter a time value, such 
        /// as Reverb Decay Time.
        /// </summary>
        TIME = 0x60000000,

        /// <summary>
        /// A control that is adjusted by a listbox containing numerous 
        /// "values" to be selected. The user will single-select, or perhaps 
        /// multiple-select if desired, his choice of value(s).
        /// </summary>
        LIST = 0x70000000
    }

    /// <summary>
    /// Flags of a mixer control.
    /// </summary>
    [Flags]
    public enum MixerControlFlags
    {
        /// <summary>
        /// This control has multiple channels, but only one value for
        /// all of them.
        /// </summary>
        UNIFORM = 0x00000001,

        /// <summary>
        /// This control has multiple values for one channel (like an equalizer).
        /// </summary>
        MULTIPLE = 0x00000002,

        /// <summary>
        /// This control is disabled.
        /// </summary>
        DISABLED = unchecked((int) 0x80000000)
    }

    /// <summary>
    /// The type of a mixer control.
    /// You can find descriptions for most of these types on 
    /// http://www.borg.com/~jglatt/tech/mixer.htm.
    /// </summary>
    public enum MixerControlType
    {
        ///
        MIXERCONTROL_CT_SC_SWITCH_BOOLEAN = 0x00000000,

        ///
        MIXERCONTROL_CT_SC_SWITCH_BUTTON = 0x01000000,

        ///
        MIXERCONTROL_CT_SC_METER_POLLED = 0x00000000,

        ///
        MIXERCONTROL_CT_SC_TIME_MICROSECS = 0x00000000,

        ///
        MIXERCONTROL_CT_SC_TIME_MILLISECS = 0x01000000,

        ///
        MIXERCONTROL_CT_SC_LIST_SINGLE = 0x00000000,

        ///
        MIXERCONTROL_CT_SC_LIST_MULTIPLE = 0x01000000,

        ///
        MIXERCONTROL_CT_UNITS_CUSTOM = 0x00000000,

        ///
        MIXERCONTROL_CT_UNITS_BOOLEAN = 0x00010000,

        ///
        MIXERCONTROL_CT_UNITS_SIGNED = 0x00020000,

        ///
        MIXERCONTROL_CT_UNITS_UNSIGNED = 0x00030000,

        ///
        MIXERCONTROL_CT_UNITS_DECIBELS = 0x00040000, /* in 10ths */

        ///
        MIXERCONTROL_CT_UNITS_PERCENT = 0x00050000, /* in 10ths */

        ///
        MIXERCONTROL_CONTROLTYPE_CUSTOM = (MixerControlClass.CUSTOM | MIXERCONTROL_CT_UNITS_CUSTOM),

        ///
        MIXERCONTROL_CONTROLTYPE_BOOLEANMETER =
            (MixerControlClass.METER | MIXERCONTROL_CT_SC_METER_POLLED | MIXERCONTROL_CT_UNITS_BOOLEAN),

        ///
        MIXERCONTROL_CONTROLTYPE_SIGNEDMETER =
            (MixerControlClass.METER | MIXERCONTROL_CT_SC_METER_POLLED | MIXERCONTROL_CT_UNITS_SIGNED),

        ///
        MIXERCONTROL_CONTROLTYPE_PEAKMETER = (MIXERCONTROL_CONTROLTYPE_SIGNEDMETER + 1),

        ///
        MIXERCONTROL_CONTROLTYPE_UNSIGNEDMETER =
            (MixerControlClass.METER | MIXERCONTROL_CT_SC_METER_POLLED | MIXERCONTROL_CT_UNITS_UNSIGNED),

        ///
        MIXERCONTROL_CONTROLTYPE_BOOLEAN = (MixerControlClass.SWITCH | MIXERCONTROL_CT_SC_SWITCH_BOOLEAN |
                                            MIXERCONTROL_CT_UNITS_BOOLEAN),

        ///
        MIXERCONTROL_CONTROLTYPE_ONOFF = (MIXERCONTROL_CONTROLTYPE_BOOLEAN + 1),

        ///
        MIXERCONTROL_CONTROLTYPE_MUTE = (MIXERCONTROL_CONTROLTYPE_BOOLEAN + 2),

        ///
        MIXERCONTROL_CONTROLTYPE_MONO = (MIXERCONTROL_CONTROLTYPE_BOOLEAN + 3),

        ///
        MIXERCONTROL_CONTROLTYPE_LOUDNESS = (MIXERCONTROL_CONTROLTYPE_BOOLEAN + 4),

        ///
        MIXERCONTROL_CONTROLTYPE_STEREOENH = (MIXERCONTROL_CONTROLTYPE_BOOLEAN + 5),

        ///
        MIXERCONTROL_CONTROLTYPE_SLIDER = (MixerControlClass.SLIDER | MIXERCONTROL_CT_UNITS_SIGNED),

        ///
        MIXERCONTROL_CONTROLTYPE_FADER = (MixerControlClass.FADER | MIXERCONTROL_CT_UNITS_UNSIGNED),

        ///
        MIXERCONTROL_CONTROLTYPE_VOLUME = (MIXERCONTROL_CONTROLTYPE_FADER + 1),

        ///
        MIXERCONTROL_CONTROLTYPE_BASS = (MIXERCONTROL_CONTROLTYPE_FADER + 2),

        ///
        MIXERCONTROL_CONTROLTYPE_TREBLE = (MIXERCONTROL_CONTROLTYPE_FADER + 3),

        ///
        MIXERCONTROL_CONTROLTYPE_EQUALIZER = (MIXERCONTROL_CONTROLTYPE_FADER + 4),

        ///
        MIXERCONTROL_CONTROLTYPE_SINGLESELECT =
            (MixerControlClass.LIST | MIXERCONTROL_CT_SC_LIST_SINGLE | MIXERCONTROL_CT_UNITS_BOOLEAN),

        ///
        MIXERCONTROL_CONTROLTYPE_MUX = (MIXERCONTROL_CONTROLTYPE_SINGLESELECT + 1),

        ///
        MIXERCONTROL_CONTROLTYPE_MULTIPLESELECT =
            (MixerControlClass.LIST | MIXERCONTROL_CT_SC_LIST_MULTIPLE | MIXERCONTROL_CT_UNITS_BOOLEAN),

        ///
        MIXERCONTROL_CONTROLTYPE_MIXER = (MIXERCONTROL_CONTROLTYPE_MULTIPLESELECT + 1),

        ///
        MIXERCONTROL_CONTROLTYPE_MICROTIME =
            (MixerControlClass.TIME | MIXERCONTROL_CT_SC_TIME_MICROSECS | MIXERCONTROL_CT_UNITS_UNSIGNED),

        ///
        MIXERCONTROL_CONTROLTYPE_MILLITIME =
            (MixerControlClass.TIME | MIXERCONTROL_CT_SC_TIME_MILLISECS | MIXERCONTROL_CT_UNITS_UNSIGNED),
    }
}