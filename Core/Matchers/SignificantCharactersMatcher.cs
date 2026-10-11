using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TaskSwitcher.Core.Matchers
{
    public class SignificantCharactersMatcher : IMatcher
    {
        internal const int PatternCacheCapacity = 128;

        private static readonly object PatternCacheLock = new();
        private static readonly Dictionary<string, CachedRegex> CachedPatterns = new(StringComparer.Ordinal);
        private static readonly LinkedList<string> PatternUsage = new();

        public MatchResult Evaluate(string input, string pattern)
        {
            if (input == null || pattern == null)
            {
                return NonMatchResult(input);
            }

            Regex regex = GetCachedRegex(pattern);

            Match match = regex.Match(input);

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

        private static Regex GetCachedRegex(string pattern)
        {
            lock (PatternCacheLock)
            {
                if (CachedPatterns.TryGetValue(pattern, out CachedRegex cached))
                {
                    PatternUsage.Remove(cached.UsageNode);
                    PatternUsage.AddFirst(cached.UsageNode);
                    return cached.Regex;
                }

                // Deliberately not RegexOptions.Compiled: every keystroke produces a new pattern
                // that is matched against a few dozen titles, and compiling one costs far more
                // (roughly 15 ms) than interpreting it for all of them (roughly 2 ms).
                string regexPattern = BuildRegexPattern(pattern);
                Regex regex = new(regexPattern);

                if (CachedPatterns.Count == PatternCacheCapacity)
                {
                    LinkedListNode<string> leastRecentlyUsed = PatternUsage.Last;
                    PatternUsage.RemoveLast();
                    CachedPatterns.Remove(leastRecentlyUsed.Value);
                }

                LinkedListNode<string> usageNode = PatternUsage.AddFirst(pattern);
                CachedPatterns.Add(pattern, new CachedRegex(regex, usageNode));

                return regex;
            }
        }

        internal static int CachedPatternCount
        {
            get
            {
                lock (PatternCacheLock)
                {
                    return CachedPatterns.Count;
                }
            }
        }

        internal static void ClearPatternCache()
        {
            lock (PatternCacheLock)
            {
                CachedPatterns.Clear();
                PatternUsage.Clear();
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

        private sealed record CachedRegex(Regex Regex, LinkedListNode<string> UsageNode);
    }
}
