using System.Collections.Generic;

namespace ManagedWinapi.Windows.Contents
{
    internal class ContentParserRegistry
    {
        private static ContentParserRegistry instance;

        public static ContentParserRegistry Instance => instance ?? (instance = new ContentParserRegistry());

        private readonly List<WindowContentParser> parsers = new();

        private ContentParserRegistry()
        {
            parsers.Add(new ComboBoxParser());
            parsers.Add(new ListBoxParser());
            parsers.Add(new TextFieldParser(true));
            parsers.Add(new ListViewParser());
            parsers.Add(new TreeViewParser());
            parsers.Add(new AccessibleWindowParser());
            parsers.Add(new TextFieldParser(false));
        }

        public WindowContentParser GetParser(SystemWindow sw)
        {
            foreach (WindowContentParser parser in parsers)
            {
                if (parser.CanParseContent(sw))
                    return parser;
            }

            return null;
        }
    }
}
