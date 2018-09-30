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
            string filterText = query;
            string processFilterText = null;

            string[] queryParts = query.Split(new[] {'.'}, 2);

            if (queryParts.Length != 2)
            {
                return context.Windows
                    .Select(
                        w =>
                            new
                            {
                                Window = w,
                                ResultsTitle = Score(w.WindowTitle, filterText),
                                ResultsProcessTitle = Score(w.ProcessTitle, processFilterText ?? filterText)
                            })
                    .Where(predicate: r =>
                    {
                        if (processFilterText == null)
                        {
                            return r.ResultsTitle.Any(wt => wt.Matched) || r.ResultsProcessTitle.Any(pt => pt.Matched);
                        }

                        return r.ResultsTitle.Any(wt => wt.Matched) && r.ResultsProcessTitle.Any(pt => pt.Matched);
                    })
                    .OrderByDescending(r =>
                        r.ResultsTitle.Sum(wt => wt.Score) + r.ResultsProcessTitle.Sum(pt => pt.Score))
                    .Select(
                        r =>
                            new FilterResult<T>
                            {
                                AppWindow = r.Window,
                                WindowTitleMatchResults = r.ResultsTitle,
                                ProcessTitleMatchResults = r.ResultsProcessTitle
                            });
            }

            processFilterText = queryParts[0];
            if (processFilterText.Length == 0)
            {
                processFilterText = context.ForegroundWindowProcessTitle;
            }

            filterText = queryParts[1];

            return context.Windows
                .Select(
                    w =>
                        new
                        {
                            Window = w,
                            ResultsTitle = Score(w.WindowTitle, filterText),
                            ResultsProcessTitle = Score(w.ProcessTitle, processFilterText ?? filterText)
                        })
                .Where(r =>
                {
                    if (processFilterText == null)
                    {
                        return r.ResultsTitle.Any(wt => wt.Matched) || r.ResultsProcessTitle.Any(pt => pt.Matched);
                    }
                    return r.ResultsTitle.Any(wt => wt.Matched) && r.ResultsProcessTitle.Any(pt => pt.Matched);
                })
                .OrderByDescending(r => r.ResultsTitle.Sum(wt => wt.Score) + r.ResultsProcessTitle.Sum(pt => pt.Score))
                .Select(
                    r =>
                        new FilterResult<T>
                        {
                            AppWindow = r.Window,
                            WindowTitleMatchResults = r.ResultsTitle,
                            ProcessTitleMatchResults = r.ResultsProcessTitle
                        });
        }

        private static List<MatchResult> Score(string title, string filterText)
        {
            StartsWithMatcher startsWithMatcher = new StartsWithMatcher();
            ContainsMatcher containsMatcher = new ContainsMatcher();
            SignificantCharactersMatcher significantCharactersMatcher = new SignificantCharactersMatcher();
            IndividualCharactersMatcher individualCharactersMatcher = new IndividualCharactersMatcher();

            List<MatchResult> results = new List<MatchResult>
            {
                startsWithMatcher.Evaluate(title, filterText),
                significantCharactersMatcher.Evaluate(title, filterText),
                containsMatcher.Evaluate(title, filterText),
                individualCharactersMatcher.Evaluate(title, filterText)
            };

            return results;
        }
    }

    public class WindowFilterContext<T> where T : IWindowText
    {
        public string ForegroundWindowProcessTitle { get; set; }
        public IEnumerable<T> Windows { get; set; } 
    }
}