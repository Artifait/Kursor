// SettingsWindow.xaml.cs
using KursorClient.Services;
using System;
using System.Windows;

namespace KursorClient.Windows
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            // при открытии заполним textbox значением из конфига
            try
            {
                var cur = ConfigService.GetString("server", "");
                ServerUrlBox.Text = cur;
            }
            catch
            {
                ServerUrlBox.Text = "";
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var txt = ServerUrlBox.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(txt))
            {
                MessageBox.Show("Введите server:port");
                return;
            }

            try
            {
                // попытаемся сохранить и распарсить
                ConfigService.SetServerFromString(txt);

                MessageBox.Show("Сохранено");
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Неверный адрес: " + ex.Message);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
