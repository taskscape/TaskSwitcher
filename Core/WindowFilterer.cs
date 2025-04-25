using System.Collections.Generic;
using System.Linq;
using TaskSwitcher.Core.Matchers;

namespace TaskSwitcher.Core
{
    public class WindowFilterer
    {
        // Threshold for window count above which parallel processing will be used
        private const int ParallelProcessingThreshold = 30;

        public static IEnumerable<FilterResult<T>> Filter<T>(WindowFilterContext<T> context, string query)
            where T : IWindowText
        {
            // Parse the query into filter texts
            (string filterText, string processFilterText) = ParseQuery(query, context.ForegroundWindowProcessTitle);

            // Use standard LINQ for small window lists
            List<T> windowsList = context.Windows.ToList();
            return windowsList.Count < ParallelProcessingThreshold ? FilterSequential(windowsList, filterText, processFilterText) :
                // Use parallel processing for larger window lists
                FilterParallel(windowsList, filterText, processFilterText);
        }
        
        private static IEnumerable<FilterResult<T>> FilterSequential<T>(List<T> windows, string filterText, string processFilterText)
            where T : IWindowText
        {
            return windows
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
        
        private static IEnumerable<FilterResult<T>> FilterParallel<T>(List<T> windows, string filterText, string processFilterText)
            where T : IWindowText
        {
            return windows
                .AsParallel()
                .Select(w => CreateFilterResult(w, filterText, processFilterText))
                .Where(r => ShouldIncludeWindow(r.ResultsTitle, r.ResultsProcessTitle, processFilterText))
                .AsSequential() // Return to sequential for ordering operations
                .OrderByDescending(r => r.ResultsTitle.Sum(wt => wt.Score) + r.ResultsProcessTitle.Sum(pt => pt.Score))
                .Select(r => new FilterResult<T>
                {
                    AppWindow = r.Window,
                    WindowTitleMatchResults = r.ResultsTitle,
                    ProcessTitleMatchResults = r.ResultsProcessTitle
                });
        }

        private static (string filterText, string processFilterText) ParseQuery(string query, string foregroundWindowProcessTitle)
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

        private static bool ShouldIncludeWindow(List<MatchResult> titleResults, List<MatchResult> processTitleResults, string processFilterText)
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
        public T Window { get; init; }
        public List<MatchResult> ResultsTitle { get; init; }
        public List<MatchResult> ResultsProcessTitle { get; init; }
    }

    public class WindowFilterContext<T> where T : IWindowText
    {
        public string ForegroundWindowProcessTitle { get; init; }
        public IEnumerable<T> Windows { get; init; } 
    }
}