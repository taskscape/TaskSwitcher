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
            Process.Start(new ProcessStartInfo(navigateUri));
            e.Handled = true;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}