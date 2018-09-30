using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using ManagedWinapi.Accessibility;

namespace ManagedWinapi.Windows.Contents
{
    /// <summary>
    /// The content of an object that supports the Accessibility API 
    /// (used by screen readers and similar programs).
    /// </summary>
    public class AccessibleWindowContent : WindowContent
    {

        bool parsed = false;
        readonly string name;
        string menu, sysmenu, clientarea;
        readonly bool hasMenu, hasSysMenu, hasClientArea;
        SystemWindow sw;

        internal AccessibleWindowContent(string name, bool hasMenu, bool hasSysMenu, bool hasClientArea, SystemWindow sw)
        {
            this.name = name;
            this.hasMenu = hasMenu;
            this.hasSysMenu = hasSysMenu;
            this.hasClientArea = hasClientArea;
            this.sw = sw;
        }

        /// <inheritdoc />
        public string ComponentType => "AccessibleWindow";

        /// <inheritdoc />
        public string ShortDescription => name + " <AccessibleWindow:" +
                                          (hasSysMenu ? " SystemMenu" : "") +
                                          (hasMenu ? " Menu" : "") +
                                          (hasClientArea ? " ClientArea" : "") + ">";

        /// <inheritdoc />
        public string LongDescription
        {
            get
            {
                ParseIfNeeded();
                string result = ShortDescription + "\n";
                if (sysmenu != null)
                    result += "System menu:\n" + sysmenu + "\n";
                if (menu != null)
                    result += "Menu:\n" + menu + "\n";
                if (clientarea != null)
                    result += "Client area:\n" + clientarea + "\n";
                return result;
            }
        }

        private void ParseIfNeeded()
        {
            if (parsed) return;
            if (hasSysMenu) sysmenu = ParseMenu(sw, AccessibleObjectID.OBJID_SYSMENU);
            if (hasMenu) menu = ParseMenu(sw, AccessibleObjectID.OBJID_MENU);
            if (hasClientArea) clientarea = ParseClientArea(sw);
            parsed = true;
        }

        private string ParseMenu(SystemWindow systemWindow, AccessibleObjectID accessibleObjectID)
        {
            SystemAccessibleObject sao = SystemAccessibleObject.FromWindow(systemWindow, accessibleObjectID);
            StringBuilder menuItems = new StringBuilder();
            ParseSubMenu(menuItems, sao, 1);
            return menuItems.ToString();
        }

        private void ParseSubMenu(StringBuilder menuitems, SystemAccessibleObject sao, int depth)
        {
            foreach (SystemAccessibleObject c in sao.Children)
            {
                if (c.RoleIndex != 11 && c.RoleIndex != 12)
                {
                    continue;
                }

                menuitems.Append(ListContent.Repeat('\t', depth) + c.Name + "\n");
                ParseSubMenu(menuitems, c, depth + 1);
            }
        }

        private string ParseClientArea(SystemWindow sw)
        {
            SystemAccessibleObject sao = SystemAccessibleObject.FromWindow(sw, AccessibleObjectID.OBJID_CLIENT);
            StringBuilder sb = new StringBuilder();
            ParseClientAreaElement(sb, sao, 1);
            return sb.ToString();
        }

        private void ParseClientAreaElement(StringBuilder stringBuilder, SystemAccessibleObject systemAccessibleObject, int depth)
        {
            stringBuilder.Append("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~\n");
            stringBuilder.Append(ListContent.Repeat('*', depth) + " " + systemAccessibleObject + "\n");
            try
            {
                stringBuilder.Append("D: " + systemAccessibleObject.Description + "\n");
            }
            catch (COMException) { }
            try
            {
                stringBuilder.Append("V: " + systemAccessibleObject.Value + "\n");
            }
            catch (COMException) { }
            foreach (SystemAccessibleObject c in systemAccessibleObject.Children)
            {
                if (c.Window == systemAccessibleObject.Window)
                    ParseClientAreaElement(stringBuilder, c, depth + 1);
            }
        }

        ///
        public Dictionary<string, string> PropertyList
        {
            get
            {
                Dictionary<string, string> result = new Dictionary<string, string>();
                return result;
            }
        }

    }

    class AccessibleWindowParser : WindowContentParser
    {
        internal override bool CanParseContent(SystemWindow systemWindow)
        {
            return TestMenu(systemWindow, AccessibleObjectID.OBJID_MENU) ||
                TestMenu(systemWindow, AccessibleObjectID.OBJID_SYSMENU) ||
                TestClientArea(systemWindow);
        }

        protected override WindowContent ParseContent(SystemWindow systemWindow)
        {
            SystemAccessibleObject sao = SystemAccessibleObject.FromWindow(systemWindow, AccessibleObjectID.OBJID_WINDOW);
            bool systemMenu = TestMenu(systemWindow, AccessibleObjectID.OBJID_SYSMENU);
            bool menu = TestMenu(systemWindow, AccessibleObjectID.OBJID_MENU);
            bool clientArea = TestClientArea(systemWindow);
            return new AccessibleWindowContent(sao.Name, menu, systemMenu, clientArea, systemWindow);
        }

        private bool TestClientArea(SystemWindow sw)
        {
            try
            {
                SystemAccessibleObject sao = SystemAccessibleObject.FromWindow(sw, AccessibleObjectID.OBJID_CLIENT);
                foreach (SystemAccessibleObject c in sao.Children)
                {
                    if (c.Window == sw) return true;
                }
            }
            catch (COMException) { }
            return false;
        }

        private bool TestMenu(SystemWindow systemWindow, AccessibleObjectID accessibleObjectID)
        {
            SystemAccessibleObject systemAccessibleObject = SystemAccessibleObject.FromWindow(systemWindow, accessibleObjectID);
            return systemAccessibleObject.Children.Length > 0;
        }
    }
}
