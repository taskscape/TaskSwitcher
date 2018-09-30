namespace TaskSwitcher.Core.Matchers
{
    public interface IMatcher
    {
        MatchResult Evaluate(string input, string pattern);
    }
}