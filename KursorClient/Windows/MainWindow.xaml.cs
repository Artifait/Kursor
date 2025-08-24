using System.Windows;
using KursorClient.Windows;

namespace KursorClient.Windows
{
    public partial class MainWindow : Window
    {
        public MainWindow() { InitializeComponent(); }
        private void TeacherBtn_Click(object sender, RoutedEventArgs e)
        {
            var w = new TeacherSetupWindow();
            w.Show();
            this.Close();
        }
        private void StudentBtn_Click(object sender, RoutedEventArgs e)
        {
            var w = new StudentSetupWindow();
            w.Show();
            this.Close();
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var settings = new SettingsWindow { Owner = this };
            settings.ShowDialog();
        }
    }
}