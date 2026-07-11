using System.Reflection;
using System.Windows;
using System.Diagnostics;
using System.Windows.Documents;

namespace TaskSwitcher
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            VersionNumber.Inlines.Add(Assembly.GetEntryAssembly().GetName().Version.ToString());
        }

        private void HandleRequestNavigate(object sender, RoutedEventArgs e)
        {
            Hyperlink hyperlink = e.OriginalSource as Hyperlink;
            if (hyperlink == null) return;

            string navigateUri = hyperlink.NavigateUri.ToString();
            e.Handled = true;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = navigateUri,
                    UseShellExecute = true
                });
            }
            catch (System.Exception ex)
            {
                DiagnosticLogger.LogException("AboutWindow.HandleRequestNavigate", ex);
                MessageBox.Show(
                    "TaskSwitcher could not open this link in your default browser.",
                    "Unable to Open Browser",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
