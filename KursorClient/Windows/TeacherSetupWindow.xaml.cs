using System;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using KursorClient.Utils;
using KursorClient.Services;
using System.Net.Http.Json;

namespace KursorClient.Windows
{
    public partial class TeacherSetupWindow : Window
    {
        private string? _token;
        private SignalRService? _signalR;
        public TeacherSetupWindow() { InitializeComponent(); }
        private async void CreateRoom_Click(object sender, RoutedEventArgs e)
        {
            var server = ConfigReader.ReadServerUrl();
            try
            {
                using var http = new HttpClient();
                var req = new { AspectW = 16, AspectH = 9 };
                var resp = await http.PostAsJsonAsync($"{server}/api/rooms",
                req);
                resp.EnsureSuccessStatusCode();
                var doc = await
                resp.Content.ReadFromJsonAsync<JsonElement>();
                var link = doc.GetProperty("link").GetString();
                _token = doc.GetProperty("token").GetString();
                LinkBox.Text = link ?? "";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка создания комнаты: " + ex.Message);
            }
        }
        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(LinkBox.Text))
                Clipboard.SetText(LinkBox.Text);
        }
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            var main = new MainWindow();
            main.Show();
            this.Close();
        }
        // После того как ученик подключился — учитель запускает рабочее окно вручную(в этом примере простая модель)
        private async void StartWorkWindow(object sender, RoutedEventArgs e)
        {
            var server = ConfigReader.ReadServerUrl();
            _signalR = new SignalRService(server);

            await _signalR.StartAsync();
            if (!string.IsNullOrEmpty(_token)) await
            _signalR.JoinRoomAsync(_token, "teacher");
            var w = new TeacherWorkWindow(_signalR, _token!);
            w.Show();
            this.Close();
        }
    }
}
