﻿using System.Text.RegularExpressions;

namespace TaskSwitcher.Core.Matchers
{
    public class ContainsMatcher : IMatcher
    {
        public MatchResult Evaluate(string input, string pattern)
        {
            if (input == null || pattern == null)
            {
                return NonMatchResult(input);
            }

            Match match = Regex.Match(input, "(.*)(" + Regex.Escape(pattern) + ")(.*)", RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                return NonMatchResult(input);
            }

            MatchResult matchResult = new();
            if (match.Groups[1].Length > 0)
            {
                matchResult.StringParts.Add(new StringPart(match.Groups[1].Value));
            }

            if (match.Groups[2].Length > 0)
            {
                matchResult.StringParts.Add(new StringPart(match.Groups[2].Value, true));
            }

            if (match.Groups[3].Length > 0)
            {
                matchResult.StringParts.Add(new StringPart(match.Groups[3].Value));
            }

            matchResult.Matched = true;
            matchResult.Score = 2;

            return matchResult;
        }

        private static MatchResult NonMatchResult(string input)
        {
            MatchResult matchResult = new();
            if (input != null)
            {
                matchResult.StringParts.Add(new StringPart(input));
            }
            return matchResult;
        }
    }
}