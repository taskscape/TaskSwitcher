using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using ManagedWinapi.Accessibility;

namespace ManagedWinapi.Windows.Contents
{
    /// <summary>
    /// The content of a list box or combo box.
    /// </summary>
    public class ListContent : WindowContent
    {
        private string[] values;

        internal ListContent(string type, int selected, string current, string[] values)
        {
            this.ComponentType = type;
            this.SelectedIndex = selected;
            this.SelectedValue = current;
            this.values = values;
        }

        ///
        public string ComponentType { get; }

        ///
        public string ShortDescription => (SelectedValue == null ? "" : SelectedValue + " ") + "<" + ComponentType + ">";

        ///
        public string LongDescription
        {
            get
            {
                StringBuilder sb = new();
                sb.Append("<" + ComponentType + ">");
                if (SelectedValue != null)
                    sb.Append(" (selected value: \"" + SelectedValue + "\")");
                sb.Append("\nAll values:\n");
                int idx = 0;
                foreach (string v in values)
                {
                    if (SelectedIndex == idx) sb.Append("*");
                    sb.Append("\t" + v + "\n");
                    idx++;
                }
                return sb.ToString();
            }
        }

        ///
        public Dictionary<string, string> PropertyList
        {
            get
            {
                Dictionary<string, string> result = new();
                result.Add("SelectedValue", SelectedValue);
                result.Add("SelectedIndex", "" + SelectedIndex);
                result.Add("Count", "" + values.Length);
                for (int i = 0; i < values.Length; i++)
                {
                    result.Add("Value" + i, values[i]);
                }
                return result;
            }
        }

        /// <summary>
        /// The value in this list or combo box that is selected.
        /// In a combo box, this value may not be in the list.
        /// </summary>
        public String SelectedValue { get; }

        /// <summary>
        /// The index of the selected item, or -1 if no item
        /// is selected.
        /// </summary>
        public int SelectedIndex { get; }

        /// <summary>
        /// The number of items in this list.
        /// </summary>
        public int Count => values.Length;

        /// <summary>
        /// Accesses individual list items.
        /// </summary>
        /// <param name="index">Index of list item.</param>
        /// <returns>The list item.</returns>
        public string this[int index] => values[index];

        internal static string Repeat(char ch, int count)
        {
            char[] tmp = new char[count];
            for (int i = 0; i < tmp.Length; i++)
            {
                tmp[i] = ch;
            }
            return new string(tmp);
        }
    }

    internal class ListBoxParser : WindowContentParser
    {
        internal override bool CanParseContent(SystemWindow sw)
        {
            return SystemListBox.FromSystemWindow(sw) != null;
        }

        protected override WindowContent ParseContent(SystemWindow sw)
        {
            SystemListBox slb = SystemListBox.FromSystemWindow(sw);
            int c = slb.Count;
            string[] values = new string[c];
            for (int i = 0; i < c; i++)
            {
                values[i] = slb[i];
            }
            return new ListContent("ListBox", slb.SelectedIndex, slb.SelectedItem, values);
        }
    }

    internal class ComboBoxParser : WindowContentParser
    {
        internal override bool CanParseContent(SystemWindow sw)
        {
            return SystemComboBox.FromSystemWindow(sw) != null;
        }

        protected override WindowContent ParseContent(SystemWindow sw)
        {
            SystemComboBox slb = SystemComboBox.FromSystemWindow(sw);
            int c = slb.Count;
            string[] values = new string[c];
            for (int i = 0; i < c; i++)
            {
                values[i] = slb[i];
            }
            return new ListContent("ComboBox", -1, sw.Title, values);
        }
    }

    internal class ListViewParser : WindowContentParser
    {

        internal override bool CanParseContent(SystemWindow sw)
        {
            uint LVM_GETITEMCOUNT = (0x1000 + 4);
            int cnt = sw.SendGetMessage(LVM_GETITEMCOUNT);
            return cnt != 0;
        }

        protected override WindowContent ParseContent(SystemWindow sw)
        {
            uint LVM_GETITEMCOUNT = (0x1000 + 4);
            int cnt = sw.SendGetMessage(LVM_GETITEMCOUNT);
            if (cnt == 0) throw new Exception();
            SystemAccessibleObject o = SystemAccessibleObject.FromWindow(sw, AccessibleObjectID.OBJID_CLIENT);
            if (o.RoleIndex == 33)
            {
                // are there column headers?
                int cs = o.Children.Length;
                string[] hdr = null;
                if (cs > 0)
                {
                    SystemAccessibleObject headers = o.Children[cs - 1];
                    if (headers.RoleIndex == 9 && headers.Window != sw)
                    {
                        SystemAccessibleObject hdrL = SystemAccessibleObject.FromWindow(headers.Window, AccessibleObjectID.OBJID_CLIENT);
                        hdr = new string[hdrL.Children.Length];
                        for (int i = 0; i < hdr.Length; i++)
                        {
                            if (hdrL.Children[i].RoleIndex != 25)
                            {
                                hdr = null;
                                break;
                            }
                            hdr[i] = hdrL.Children[i].Name;
                        }
                        if (hdr != null)
                        {
                            cs--;
                        }
                    }
                }
                List<string> values = new();
                for (int i = 0; i < cs; i++)
                {
                    if (o.Children[i].RoleIndex == 34)
                    {
                        string name = o.Children[i].Name;
                        if (hdr != null)
                        {
                            try
                            {
                                string cols = o.Children[i].Description;
                                if (cols == null && values.Count == 0) { hdr = null; }
                                else
                                {
                                    string tmpCols = "; " + cols;
                                    List<string> usedHdr = new();
                                    foreach (string header in hdr)
                                    {
                                        string h = "; " + header + ": ";
                                        if (tmpCols.Contains(h))
                                        {
                                            usedHdr.Add(header);
                                            tmpCols = tmpCols.Substring(tmpCols.IndexOf(h) + h.Length);
                                        }
                                    }
                                    foreach (string header in hdr)
                                    {
                                        name += "\t";
                                        if (usedHdr.Count > 0 && usedHdr[0] == header)
                                        {
                                            if (!cols.StartsWith(header + ": "))
                                                throw new Exception();
                                            cols = cols.Substring(header.Length + 1);
                                            string elem;
                                            if (usedHdr.Count > 1)
                                            {
                                                int pos = cols.IndexOf("; " + usedHdr[1] + ": ");
                                                elem = cols.Substring(0, pos);
                                                cols = cols.Substring(pos + 2);
                                            }
                                            else
                                            {
                                                elem = cols;
                                                cols = "";
                                            }
                                            name += elem;
                                            usedHdr.RemoveAt(0);
                                        }
                                    }
                                }
                            }
                            catch (COMException ex)
                            {
                                if (ex.ErrorCode == -2147352573 && values.Count == 0)
                                {
                                    hdr = null;
                                }
                                else
                                {
                                    throw ex;
                                }
                            }
                        }
                        values.Add(name);
                    }
                }
                if (hdr != null)
                {
                    string lines = "", headers = "";
                    foreach (string h in hdr)
                    {
                        if (lines.Length > 0) lines += "\t";
                        if (headers.Length > 0) headers += "\t";
                        headers += h;
                        lines += ListContent.Repeat('~', h.Length);
                    }
                    values.Insert(0, lines);
                    values.Insert(0, headers);
                    return new ListContent("DetailsListView", -1, null, values.ToArray());
                }
                else
                {
                    return new ListContent("ListView", -1, null, values.ToArray());
                }
            }
            else
            {
                return new ListContent("EmptyListView", -1, null, new string[0]);
            }
        }
    }

    internal class TreeViewParser : WindowContentParser
    {
        private uint TVM_GETCOUNT = 0x1100 + 5;

        internal override bool CanParseContent(SystemWindow sw)
        {
            int cnt = sw.SendGetMessage(TVM_GETCOUNT, 0);
            return cnt != 0;
        }

        protected override WindowContent ParseContent(SystemWindow sw)
        {
            SystemAccessibleObject sao = SystemAccessibleObject.FromWindow(sw, AccessibleObjectID.OBJID_CLIENT);
            if (sao.RoleIndex == 35)
            {
                List<string> treeNodes = new();
                int selected = -1;
                foreach(SystemAccessibleObject n in sao.Children) {
                    if (n.RoleIndex == 36)
                    {
                        if ((n.State & 0x2) != 0)
                        {
                            selected = treeNodes.Count;
                        }
                        treeNodes.Add(ListContent.Repeat('\t', int.Parse(n.Value)) + n.Name);
                    }
                }
                if (treeNodes.Count > 0)
                {
                    return new ListContent("TreeView", selected, null, treeNodes.ToArray());
                }
            }
            return new ListContent("EmptyTreeView", -1, null, new string[0]);
        }
    }
}
