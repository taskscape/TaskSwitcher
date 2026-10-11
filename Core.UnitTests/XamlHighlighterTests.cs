using NUnit.Framework;
using TaskSwitcher.Core.Matchers;

namespace TaskSwitcher.Core.UnitTests
{
    [TestFixture]
    public class XamlHighlighterTests
    {
        [Test]
        public void DoesItWork()
        {
            StringPart input = new("test > test-1", true);
            string output = new XamlHighlighter().Highlight(new[] {input, new StringPart("test"),});
            Assert.That(output, Is.EqualTo("<Bold>test &gt; test-1</Bold>test"));
        }

        [Test]
        public void Highlight_EscapesMarkupCharacters()
        {
            string output = new XamlHighlighter().Highlight(new[] {new StringPart("a & <b>")});
            Assert.That(output, Is.EqualTo("a &amp; &lt;b&gt;"));
        }

        [Test]
        public void Highlight_CharactersNotAllowedInXml_AreDropped()
        {
            string output = new XamlHighlighter().Highlight(new[]
            {
                new StringPart("bell\u0007", true),
                new StringPart("lone\ud83dsurrogate\0")
            });
            Assert.That(output, Is.EqualTo("<Bold>bell</Bold>lonesurrogate"));
        }

        [Test]
        public void Highlight_SurrogatePair_IsPreserved()
        {
            string output = new XamlHighlighter().Highlight(new[] {new StringPart("ok 😀")});
            Assert.That(output, Is.EqualTo("ok 😀"));
        }

        [Test]
        public void Highlight_NullValue_IsTreatedAsEmpty()
        {
            string output = new XamlHighlighter().Highlight(new[] {new StringPart(), new StringPart("x")});
            Assert.That(output, Is.EqualTo("x"));
        }
    }
}