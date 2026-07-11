using NUnit.Framework;
using TaskSwitcher.Core.Matchers;

namespace TaskSwitcher.Core.UnitTests
{
    [TestFixture]
    public class SignificantCharactersMatcherTests
    {
        [Test]
        public void Evaluate_ManyDistinctPatterns_CacheRemainsBounded()
        {
            SignificantCharactersMatcher.ClearPatternCache();

            try
            {
                SignificantCharactersMatcher matcher = new();
                for (int index = 0; index < SignificantCharactersMatcher.PatternCacheCapacity + 20; index++)
                {
                    matcher.Evaluate("TaskSwitcher", $"pattern-{index}");
                }

                Assert.That(
                    SignificantCharactersMatcher.CachedPatternCount,
                    Is.EqualTo(SignificantCharactersMatcher.PatternCacheCapacity));
            }
            finally
            {
                SignificantCharactersMatcher.ClearPatternCache();
            }
        }
    }
}
