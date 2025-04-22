using System.Collections.Generic;
using System.Linq;
using TaskSwitcher.Core.Matchers;

namespace TaskSwitcher.Core
{
    public class WindowFilterer
    {
        public IEnumerable<FilterResult<T>> Filter<T>(WindowFilterContext<T> context, string query)
            where T : IWindowText
        {
            // Parse the query into filter texts
            (string filterText, string processFilterText) = ParseQuery(query, context.ForegroundWindowProcessTitle);

            // Use a single unified filtering approach
            return context.Windows
                .Select(w => CreateFilterResult(w, filterText, processFilterText))
                .Where(r => ShouldIncludeWindow(r.ResultsTitle, r.ResultsProcessTitle, processFilterText))
                .OrderByDescending(r => r.ResultsTitle.Sum(wt => wt.Score) + r.ResultsProcessTitle.Sum(pt => pt.Score))
                .Select(r => new FilterResult<T>
                {
                    AppWindow = r.Window,
                    WindowTitleMatchResults = r.ResultsTitle,
                    ProcessTitleMatchResults = r.ResultsProcessTitle
                });
        }

        private (string filterText, string processFilterText) ParseQuery(string query, string foregroundWindowProcessTitle)
        {
            string filterText = query;
            string processFilterText = null;

            string[] queryParts = query.Split(['.'], 2);

            if (queryParts.Length != 2) return (filterText, processFilterText);
            processFilterText = queryParts[0];
            if (processFilterText.Length == 0)
            {
                processFilterText = foregroundWindowProcessTitle;
            }
            filterText = queryParts[1];

            return (filterText, processFilterText);
        }

        private static WindowFilterResult<T> CreateFilterResult<T>(T window, string filterText, string processFilterText) where T : IWindowText
        {
            return new WindowFilterResult<T>
            {
                Window = window,
                ResultsTitle = Score(window.WindowTitle, filterText),
                ResultsProcessTitle = Score(window.ProcessTitle, processFilterText ?? filterText)
            };
        }

        private bool ShouldIncludeWindow(List<MatchResult> titleResults, List<MatchResult> processTitleResults, string processFilterText)
        {
            if (processFilterText == null)
            {
                return titleResults.Any(wt => wt.Matched) || processTitleResults.Any(pt => pt.Matched);
            }

            return titleResults.Any(wt => wt.Matched) && processTitleResults.Any(pt => pt.Matched);
        }

        private static List<MatchResult> Score(string title, string filterText)
        {
            StartsWithMatcher startsWithMatcher = new();
            ContainsMatcher containsMatcher = new();
            SignificantCharactersMatcher significantCharactersMatcher = new();
            IndividualCharactersMatcher individualCharactersMatcher = new();

            List<MatchResult> results =
            [
                startsWithMatcher.Evaluate(title, filterText),
                significantCharactersMatcher.Evaluate(title, filterText),
                containsMatcher.Evaluate(title, filterText),
                individualCharactersMatcher.Evaluate(title, filterText)
            ];

            return results;
        }
    }

    // Helper class to hold intermediate results during filtering
    internal class WindowFilterResult<T>
    {
        public T Window { get; set; }
        public List<MatchResult> ResultsTitle { get; set; }
        public List<MatchResult> ResultsProcessTitle { get; set; }
    }

    public class WindowFilterContext<T> where T : IWindowText
    {
        public string ForegroundWindowProcessTitle { get; set; }
        public IEnumerable<T> Windows { get; set; } 
    }
}