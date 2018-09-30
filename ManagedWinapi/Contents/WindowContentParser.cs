namespace ManagedWinapi.Windows.Contents
{
    internal abstract class WindowContentParser
    {
        internal abstract bool CanParseContent(SystemWindow systemWindow);
        protected abstract WindowContent ParseContent(SystemWindow systemWindow);

        internal static WindowContent Parse(SystemWindow systemWindow)
        {
            WindowContentParser parser = ContentParserRegistry.Instance.GetParser(systemWindow);
            return parser?.ParseContent(systemWindow);
        }
    }
}
