using System.Linq;
using NUnit.Framework;
using TaskSwitcher.Core.Matchers;

namespace TaskSwitcher.Core.UnitTests
{
    [TestFixture]
    public class StartsWithMatcherTests
    {
        [Test]
        public void Evaluate_InputStartsWithPattern_ResultIsMatched()
        {
            string input = "google chrome";
            string pattern = "google";

            MatchResult result = Evaluate(input, pattern);

            Assert.That(result.Matched, Is.True);
        }

        [Test]
        public void Evaluate_InputStartsWithPattern_ScoreIsFour()
        {
            string input = "google chrome";
            string pattern = "google";

            MatchResult result = Evaluate(input, pattern);

            Assert.That(result.Score, Is.EqualTo(4));
        }

        [Test]
        public void Evaluate_InputStartsWithPattern_FirstStringPartIsMatch()
        {
            string input = "google chrome";
            string pattern = "google";

            MatchResult result = Evaluate(input, pattern);

            Assert.That(result.StringParts.First().Value, Is.EqualTo("google"));
            Assert.That(result.StringParts.First().IsMatch, Is.True);
        }

        [Test]
        public void Evaluate_InputStartsWithPattern_SecondStringPartIsNotMatch()
        {
            string input = "google chrome";
            string pattern = "google";

            MatchResult result = Evaluate(input, pattern);

            Assert.That(result.StringParts.ToList()[1].Value, Is.EqualTo(" chrome"));
            Assert.That(result.StringParts.ToList()[1].IsMatch, Is.False);
        }

        [Test]
        public void Evaluate_NullInput_ReturnsNotMatchingResult()
        {
            MatchResult result = Evaluate(null, "google");
            Assert.That(result.Matched, Is.False);
        }

        [Test]
        public void Evaluate_NullPattern_ReturnsNotMatchingResult()
        {
            MatchResult result = Evaluate("google chrome", null);
            Assert.That(result.Matched, Is.False);
        }

        [Test]
        public void Evaluate_NullPattern_ReturnsOneNonMatchingStringPart()
        {
            MatchResult result = Evaluate("google chrome", null);
            Assert.That(result.StringParts.Count(), Is.EqualTo(1));
            Assert.That(result.StringParts.First().Value, Is.EqualTo("google chrome"));
            Assert.That(result.StringParts.First().IsMatch, Is.False);
        }

        [Test]
        public void Evaluate_InputContainsPattern_ReturnsNotMatchingResult()
        {
            MatchResult result = Evaluate("google chrome", "chrome");
            Assert.That(result.Matched, Is.False);
        }

        [Test]
        public void Evaluate_InputContainsPattern_ReturnsOneNonMatchingStringPart()
        {
            MatchResult result = Evaluate("google chrome", "chrome");
            Assert.That(result.StringParts.Count(), Is.EqualTo(1));
            Assert.That(result.StringParts.First().Value, Is.EqualTo("google chrome"));
            Assert.That(result.StringParts.First().IsMatch, Is.False);
        }

        [Test]
        public void Evaluate_InputStartsWithPattern_CasingIsNotChanged()
        {
            MatchResult result = Evaluate("Google Chrome", "google");
            Assert.That(result.StringParts[0].Value, Is.EqualTo("Google"));
            Assert.That(result.StringParts[1].Value, Is.EqualTo(" Chrome"));
        }

        private static MatchResult Evaluate(string input, string pattern)
        {
            StartsWithMatcher matcher = new();
            return matcher.Evaluate(input, pattern);
        }
    }
}