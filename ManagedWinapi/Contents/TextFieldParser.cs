using System.Collections.Generic;

namespace ManagedWinapi.Windows.Contents
{

    /// <summary>
    /// The content of a text box.
    /// </summary>
    public class TextContent : WindowContent
    {
        readonly string text;
        readonly bool password;
        readonly bool strict;

        internal TextContent(string text, bool password, bool strict)
        {
            this.text = text;
            this.password = password;
            this.strict = strict;
        }

        ///
        public string ComponentType
        {
            get { return strict ? "TextBox" : "Text"; }
        }

        ///
        public string ShortDescription
        {
            get
            {
                string s = strict ? " <TextBox>" : "";
                if (text.IndexOf("\n") != -1)
                    return "<MultiLine>" + s;
                if (password)
                    return text + " <Password>" + s;
                return text + s;
            }
        }

        ///
        public string LongDescription
        {
            get
            {
                if (password)
                {
                    return text + " <Password>";
                }

                return text;
            }
        }

        ///
        public Dictionary<string, string> PropertyList
        {
            get
            {
                Dictionary<string, string> result = new Dictionary<string, string>();
                result.Add("Password", password ? "True" : "False");
                result.Add("MultiLine", text.IndexOf('\n') != -1 ? "True" : "False");
                result.Add("Text", text);
                return result;
            }
        }
    }

    class TextFieldParser : WindowContentParser
    {
        readonly bool strict;

        public TextFieldParser(bool strict)
        {
            this.strict = strict;
        }

        internal override bool CanParseContent(SystemWindow systemWindow)
        {
            if (!strict) 
            {
                return systemWindow.Title != "";
            }

            uint EM_GETLINECOUNT = 0xBA;
            return systemWindow.SendGetMessage(EM_GETLINECOUNT) != 0;

        }

        protected override WindowContent ParseContent(SystemWindow systemWindow)
        {
            return new TextContent(systemWindow.Title, systemWindow.PasswordCharacter != 0, strict);
        }
    }
}
