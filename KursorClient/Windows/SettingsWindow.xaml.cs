using System.Windows;
using KursorClient.Utils;
namespace KursorClient.Windows
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            ServerUrlBox.Text = ConfigReader.ReadServerUrl();
        }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var url = ServerUrlBox.Text?.Trim();
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show("Введите адрес сервера");
                return;
            }
            ConfigReader.WriteServerUrl(url);
            MessageBox.Show("Сохранено");
            this.DialogResult = true;
            this.Close();
        }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}