﻿namespace TaskSwitcher.Core.Matchers
{
    public class StartsWithMatcher : IMatcher
    {
        public MatchResult Evaluate(string input, string pattern)
        {
            MatchResult matchResult = new MatchResult();

            if (input == null)
            {
                return matchResult;
            }

            if (pattern == null || !InputStartsWithPattern(input, pattern))
            {
                matchResult.StringParts.Add(new StringPart(input));
                return matchResult;
            }

            string matchedPart = input[..pattern.Length];
            string restOfInput = input[pattern.Length..];

            matchResult.Matched = true;
            matchResult.Score = 4;
            matchResult.StringParts.Add(new StringPart(matchedPart, true));
            matchResult.StringParts.Add(new StringPart(restOfInput, false));

            return matchResult;
        }

        private static bool InputStartsWithPattern(string input, string pattern)
        {
            return input.ToLowerInvariant().StartsWith(pattern.ToLowerInvariant());
        }
    }
}