namespace TaskSwitcher.Core.Matchers
{
    public class StringPart(string value, bool isMatch = false)
    {
        public string Value { get; } = value;
        public bool IsMatch { get; } = isMatch;
    }
}