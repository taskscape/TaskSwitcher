using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TaskSwitcher.Core.Matchers
{
    public class SignificantCharactersMatcher : IMatcher
    {
        // Cache for compiled regex patterns to avoid recompilation
        private static readonly Dictionary<string, Regex> CompiledPatterns = new();

        public MatchResult Evaluate(string input, string pattern)
        {
            if (input == null || pattern == null)
            {
                return NonMatchResult(input);
            }

            // Get or create the compiled regex pattern
            Regex compiledRegex = GetCompiledRegex(pattern);

            Match match = compiledRegex.Match(input);

            if (!match.Success)
            {
                return NonMatchResult(input);
            }

            MatchResult matchResult = new();

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

        private static Regex GetCompiledRegex(string pattern)
        {
            // Use pattern as the cache key
            lock (CompiledPatterns)
            {
                if (CompiledPatterns.TryGetValue(pattern, out Regex cachedRegex))
                {
                    return cachedRegex;
                }

                string regexPattern = BuildRegexPattern(pattern);
                
                // Compile the regex for better performance
                Regex compiledRegex = new(regexPattern, RegexOptions.Compiled);
                
                // Store in cache
                CompiledPatterns[pattern] = compiledRegex;
                
                return compiledRegex;
            }
        }

        private static string BuildRegexPattern(string pattern)
        {
            string regexPattern = "";
            foreach (char p in pattern)
            {
                char lowerP = char.ToLowerInvariant(p);
                char upperP = char.ToUpperInvariant(p);
                regexPattern += string.Format(@"([^\p{{Lu}}\s]*?\s?)(\b{0}|{1})", 
                    Regex.Escape(lowerP.ToString()),
                    Regex.Escape(upperP.ToString()));
            }
            return regexPattern;
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