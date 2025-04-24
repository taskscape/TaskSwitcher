using System.Collections.Generic;
using TaskSwitcher.Core.Matchers;

namespace TaskSwitcher.Core
{
    public class FilterResult<T> where T : IWindowText
    {
        public T AppWindow { get; init; }
        public IList<MatchResult> WindowTitleMatchResults { get; set; }
        public IList<MatchResult> ProcessTitleMatchResults { get; set; }
    }
}