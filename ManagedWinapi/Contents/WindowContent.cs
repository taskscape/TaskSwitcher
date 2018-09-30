using System;
using System.Collections.Generic;

namespace ManagedWinapi.Windows.Contents
{
    /// <summary>
    /// An abstract representation of the content of a window or control.
    /// </summary>
    public interface WindowContent
    {
        /// <summary>
        /// A short description of the type of this window.
        /// </summary>
        string ComponentType { get;}

        /// <summary>
        /// A short description of this content.
        /// </summary>
        string ShortDescription { get;}

        /// <summary>
        /// The full description of this content.
        /// </summary>
        string LongDescription { get;}

        /// <summary>
        /// A list of properties of this content.
        /// </summary>
        Dictionary<String, String> PropertyList { get;}
    }
}
