using System;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using KursorClient.Services;
using System.Net.Http.Json;

namespace KursorClient.Windows
{
    public partial class TeacherSetupWindow : Window
    {
        private UdpSession? _session;
        private string? _token;

        public TeacherSetupWindow()
        {
            InitializeComponent();
        }

        private async void CreateRoom_Click(object sender, RoutedEventArgs e)
        {
            if (ConfigService.ServerEndpoint == null)
            {
                MessageBox.Show("Укажите Server URL в настройках");
                return;
            }
            try
            {
                _session?.Dispose();
                _session = new UdpSession(ConfigService.ServerEndpoint);

                // Если в LinkBox уже есть токен — попробуем rebind, иначе обычное создание
                string? tokenFromBox = string.IsNullOrWhiteSpace(LinkBox.Text) ? null : LinkBox.Text.Trim();
                var token = await _session.CreateRoomAsync(tokenFromBox);
                if (token == null)
                {
                    MessageBox.Show("Не удалось создать / перепривязать комнату (timeout или токен неверен)");
                    return;
                }
                _token = token;
                LinkBox.Text = token;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(LinkBox.Text))
            {
                Clipboard.SetText(LinkBox.Text);
            }
        }

        private void StartWorkWindow(object sender, RoutedEventArgs e)
        {
            if (_session == null || string.IsNullOrEmpty(_token))
            {
                MessageBox.Show("Сначала создайте комнату");
                return;
            }
            var win = new TeacherWorkWindow(_session, _token);
            win.Show();
            this.Close();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            // Переход в главное меню
            var main = new MainWindow();
            main.Show();
            Close(); // закрыть TeacherWorkWindow
        }
    }
}
