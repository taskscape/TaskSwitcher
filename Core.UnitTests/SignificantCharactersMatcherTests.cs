using System.Linq;
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

        [Test]
        public void Evaluate_InitialsOfWords_MatchesAndHighlightsThem()
        {
            MatchResult result = new SignificantCharactersMatcher().Evaluate("Google Chrome", "gc");

            Assert.Multiple(() =>
            {
                Assert.That(result.Matched, Is.True);
                Assert.That(result.Score, Is.EqualTo(2));
                Assert.That(
                    result.StringParts.Where(part => part.IsMatch).Select(part => part.Value),
                    Is.EqualTo(new[] {"G", "C"}));
                Assert.That(string.Concat(result.StringParts.Select(part => part.Value)), Is.EqualTo("Google Chrome"));
            });
        }

        [Test]
        public void Evaluate_CapitalsInsideWord_MatchesAndHighlightsThem()
        {
            MatchResult result = new SignificantCharactersMatcher().Evaluate("TaskSwitcher", "ts");

            Assert.Multiple(() =>
            {
                Assert.That(result.Matched, Is.True);
                Assert.That(
                    result.StringParts.Where(part => part.IsMatch).Select(part => part.Value),
                    Is.EqualTo(new[] {"T", "S"}));
            });
        }

        [Test]
        public void Evaluate_CharactersInsideWords_DoesNotMatch()
        {
            MatchResult result = new SignificantCharactersMatcher().Evaluate("Google Chrome", "oh");

            Assert.Multiple(() =>
            {
                Assert.That(result.Matched, Is.False);
                Assert.That(result.StringParts.Select(part => part.Value), Is.EqualTo(new[] {"Google Chrome"}));
            });
        }

        [Test]
        public void Evaluate_SamePatternTwice_ReturnsSameResultFromCache()
        {
            SignificantCharactersMatcher matcher = new();

            MatchResult first = matcher.Evaluate("Google Chrome", "gc");
            MatchResult second = matcher.Evaluate("Google Chrome", "gc");

            Assert.That(
                second.StringParts.Select(part => (part.Value, part.IsMatch)),
                Is.EqualTo(first.StringParts.Select(part => (part.Value, part.IsMatch))));
        }
    }
}
