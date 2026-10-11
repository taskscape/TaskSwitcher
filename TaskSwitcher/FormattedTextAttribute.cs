using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Xml;
using System.Xml.Linq;

namespace TaskSwitcher
{
    public class FormattedTextAttribute
    {
        public static readonly DependencyProperty FormattedTextProperty = DependencyProperty.RegisterAttached(
            "FormattedText",
            typeof (string),
            typeof (FormattedTextAttribute),
            new UIPropertyMetadata("", FormattedTextChanged));

        public static void SetFormattedText(DependencyObject textBlock, string value)
        {
            textBlock.SetValue(FormattedTextProperty, value);
        }

        public static string GetFormattedText(DependencyObject textBlock)
        {
            return (string) textBlock.GetValue(FormattedTextProperty);
        }

        private static void FormattedTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TextBlock textBlock = d as TextBlock;
            if (textBlock == null)
            {
                return;
            }

            string formattedText = (string) e.NewValue ?? string.Empty;

            textBlock.Inlines.Clear();
            if (formattedText.Length == 0)
            {
                return;
            }

            // The text is the output of XamlHighlighter: escaped text with <Bold> elements around
            // the matches. It is read as plain XML because this runs on the UI thread for every
            // row of the list, and the general-purpose XamlReader is far slower for it.
            try
            {
                XElement root = XElement.Parse("<Root>" + formattedText + "</Root>", LoadOptions.PreserveWhitespace);
                foreach (XNode node in root.Nodes())
                {
                    switch (node)
                    {
                        case XElement { Name.LocalName: "Bold" } bold:
                            textBlock.Inlines.Add(new Bold(new Run(bold.Value)));
                            break;
                        case XElement element:
                            textBlock.Inlines.Add(new Run(element.Value));
                            break;
                        case XText text:
                            textBlock.Inlines.Add(new Run(text.Value));
                            break;
                    }
                }
            }
            catch (XmlException ex)
            {
                // An exception thrown here would surface as an unhandled exception of the
                // binding that set the value, so the text is shown without formatting instead.
                DiagnosticLogger.LogException("FormattedTextAttribute.FormattedTextChanged", ex);
                textBlock.Inlines.Clear();
                textBlock.Text = formattedText;
            }
        }
    }
}