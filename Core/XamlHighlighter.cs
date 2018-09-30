using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using TaskSwitcher.Core.Matchers;

namespace TaskSwitcher.Core
{
    public class XamlHighlighter
    {
        public string Highlight(IEnumerable<StringPart> stringParts)
        {
            if (stringParts == null) return string.Empty;

            XDocument xDocument = new XDocument(new XElement("Root"));
            foreach (StringPart stringPart in stringParts)
            {
                if (stringPart.IsMatch)
                {
                    xDocument.Root.Add(new XElement("Bold", stringPart.Value));
                }
                else
                {
                    xDocument.Root.Add(new XText(stringPart.Value));
                }
            }
            return string.Join("", xDocument.Root.Nodes().Select(x => x.ToString()).ToArray());
        }
    }
}