using NUnit.Framework;
using TaskSwitcher.Core.Matchers;

namespace TaskSwitcher.Core.UnitTests
{
    [TestFixture]
    public class ContainsMatcherTests
    {
        [Test]
        public void Evaluate_InputNull_ReturnsNonMatchingResult()
        {
            MatchResult result = Evaluate(null, "google");
            Assert.That(result.Matched, Is.False);
        }

        [Test]
        public void Evaluate_InputNull_ReturnsNonStringParts()
        {
            MatchResult result = Evaluate(null, "google");
            Assert.That(result.StringParts.Count, Is.EqualTo(0));
        }

        [Test]
        public void Evaluate_PatternNull_ReturnsNonMatchingResult()
        {
            MatchResult result = Evaluate("google chrome", null);
            Assert.That(result.Matched, Is.False);
        }

        [Test]
        public void Evaluate_InputDoesNotContainPattern_ScoreIsZero()
        {
            MatchResult result = Evaluate("google", "chrome");
            Assert.That(result.Score, Is.EqualTo(0));
        }

        [Test]
        public void Evaluate_InputDoesNotContainPattern_OneStringPart()
        {
            MatchResult result = Evaluate("google", "chrome");
            Assert.That(result.StringParts.Count, Is.EqualTo(1));
            Assert.That(result.StringParts[0].Value, Is.EqualTo("google"));
            Assert.That(result.StringParts[0].IsMatch, Is.False);
        }

        [Test]
        public void Evaluate_InputContainsPattern_ScoreIsTwo()
        {
            MatchResult result = Evaluate("google chrome", "chrome");
            Assert.That(result.Score, Is.EqualTo(2));
        }

        [Test]
        public void Evaluate_InputContainsPattern_ResultMatches()
        {
            MatchResult result = Evaluate("google chrome", "chrome");
            Assert.That(result.Matched, Is.True);
        }

        [Test]
        public void Evaluate_InputContainsPattern_TwoStringParts()
        {
            MatchResult result = Evaluate("google chrome", "chrome");
            Assert.That(result.StringParts.Count, Is.EqualTo(2));
        }

        [Test]
        public void Evaluate_InputContainsPattern_StringPartsAreCorrect()
        {
            MatchResult result = Evaluate("google chrome", "chrome");
            Assert.That(result.StringParts[0].Value, Is.EqualTo("google "));
            Assert.That(result.StringParts[0].IsMatch, Is.False);
            Assert.That(result.StringParts[1].Value, Is.EqualTo("chrome"));
            Assert.That(result.StringParts[1].IsMatch, Is.True);
        }

        [Test]
        public void Evaluate_InputContainsPatternInBeginning_StringPartsAreCorrect()
        {
            MatchResult result = Evaluate("google chrome", "google");
            Assert.That(result.StringParts.Count, Is.EqualTo(2));
            Assert.That(result.StringParts[0].Value, Is.EqualTo("google"));
            Assert.That(result.StringParts[0].IsMatch, Is.True);
            Assert.That(result.StringParts[1].Value, Is.EqualTo(" chrome"));
            Assert.That(result.StringParts[1].IsMatch, Is.False);
        }

        [Test]
        public void Evaluate_InputContainsPatternInMiddle_StringPartsAreCorrect()
        {
            MatchResult result = Evaluate("google chrome v28", "chrome");
            Assert.That(result.StringParts[0].Value, Is.EqualTo("google "));
            Assert.That(result.StringParts[0].IsMatch, Is.False);
            Assert.That(result.StringParts[1].Value, Is.EqualTo("chrome"));
            Assert.That(result.StringParts[1].IsMatch, Is.True);
            Assert.That(result.StringParts[2].Value, Is.EqualTo(" v28"));
            Assert.That(result.StringParts[2].IsMatch, Is.False);
        }

        [Test]
        public void Evaluate_IgnoresCasing_ReturnsMatchingResult()
        {
            MatchResult result = Evaluate("Google Chrome", "chrome");
            Assert.That(result.Matched, Is.True);
        }

        private static MatchResult Evaluate(string input, string pattern)
        {
            ContainsMatcher containsMatcher = new ContainsMatcher();
            return containsMatcher.Evaluate(input, pattern);
        }
    }
}