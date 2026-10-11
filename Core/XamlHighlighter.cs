using System.Collections.Generic;
using System.Text;
using System.Xml;
using TaskSwitcher.Core.Matchers;

namespace TaskSwitcher.Core
{
    public class XamlHighlighter
    {
        public string Highlight(IEnumerable<StringPart> stringParts)
        {
            if (stringParts == null) return string.Empty;

            StringBuilder builder = new();
            foreach (StringPart stringPart in stringParts)
            {
                if (stringPart.IsMatch)
                {
                    builder.Append("<Bold>");
                    AppendEscaped(builder, stringPart.Value);
                    builder.Append("</Bold>");
                }
                else
                {
                    AppendEscaped(builder, stringPart.Value);
                }
            }
            return builder.ToString();
        }

        private static void AppendEscaped(StringBuilder builder, string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                switch (character)
                {
                    case '&':
                        builder.Append("&amp;");
                        break;
                    case '<':
                        builder.Append("&lt;");
                        break;
                    case '>':
                        builder.Append("&gt;");
                        break;
                    case '\r':
                        builder.Append("&#xD;");
                        break;
                    default:
                        if (char.IsHighSurrogate(character) && index + 1 < value.Length &&
                            char.IsLowSurrogate(value[index + 1]))
                        {
                            builder.Append(character).Append(value[++index]);
                        }
                        else if (XmlConvert.IsXmlChar(character))
                        {
                            builder.Append(character);
                        }

                        // Anything else (control characters, unpaired surrogates) cannot be
                        // represented in XML. Window titles are arbitrary text supplied by other
                        // applications, so such characters are dropped instead of failing.
                        break;
                }
            }
        }
    }
}