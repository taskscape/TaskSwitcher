using System;
using System.Text.RegularExpressions;

namespace TaskSwitcher.Core.Matchers
{
    public class SignificantCharactersMatcher : IMatcher
    {
        public MatchResult Evaluate(string input, string pattern)
        {
            if (input == null || pattern == null)
            {
                return NonMatchResult(input);
            }

            string regexPattern = BuildRegexPattern(pattern);

            Match match = Regex.Match(input, regexPattern);

            if (!match.Success)
            {
                return NonMatchResult(input);
            }

            MatchResult matchResult = new MatchResult();

            string beforeMatch = input.Substring(0, match.Index);
            matchResult.StringParts.Add(new StringPart(beforeMatch));

            for (int groupIndex = 1; groupIndex < match.Groups.Count; groupIndex++)
            {
                Group group = match.Groups[groupIndex];
                if (group.Value.Length > 0)
                {
                    matchResult.StringParts.Add(new StringPart(group.Value, groupIndex%2 == 0));
                }
            }

            string afterMatch = input.Substring(match.Index + match.Length);
            matchResult.StringParts.Add(new StringPart(afterMatch));

            matchResult.Matched = true;
            matchResult.Score = 2;

            return matchResult;
        }

        private static string BuildRegexPattern(string pattern)
        {
            string regexPattern = "";
            foreach (char p in pattern)
            {
                char lowerP = Char.ToLowerInvariant(p);
                char upperP = Char.ToUpperInvariant(p);
                regexPattern += string.Format(@"([^\p{{Lu}}\s]*?\s?)(\b{0}|{1})", Regex.Escape(lowerP + ""),
                    Regex.Escape(upperP + ""));
            }
            return regexPattern;
        }

        private static MatchResult NonMatchResult(string input)
        {
            MatchResult matchResult = new MatchResult();
            if (input != null)
            {
                matchResult.StringParts.Add(new StringPart(input));
            }
            return matchResult;
        }
    }
}