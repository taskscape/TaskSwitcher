using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace TaskSwitcher.Core.UnitTests
{
    [TestFixture]
    public class WindowFiltererTests
    {
        [Test]
        public void Filter_ParallelEqualScoreResults_PreservesSourceOrder()
        {
            List<TestWindow> windows = Enumerable.Range(0, 50)
                .Select(index => new TestWindow($"Window {index}", "Process"))
                .ToList();
            WindowFilterContext<TestWindow> context = new()
            {
                Windows = windows,
                ForegroundWindowProcessTitle = "Process"
            };

            List<TestWindow> filteredWindows = new WindowFilterer()
                .Filter(context, string.Empty)
                .Select(result => result.AppWindow)
                .ToList();

            Assert.That(filteredWindows, Is.EqualTo(windows));
        }

        private sealed class TestWindow(string windowTitle, string processTitle) : IWindowText
        {
            public string WindowTitle { get; } = windowTitle;
            public string ProcessTitle { get; } = processTitle;
        }
    }
}
