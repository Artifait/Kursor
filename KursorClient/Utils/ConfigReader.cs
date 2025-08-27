using System;
using System.IO;

namespace KursorClient.Utils
{
    public static class ConfigReader
    {
        private const string DefaultUrl = "http://127.0.0.1:5254";
        private const string DefaultPath = "kursor.ini";

        public static string ReadServerUrl(string path = DefaultPath)
        {
            try
            {
                if (!File.Exists(path)) return DefaultUrl;
                foreach (var line in File.ReadAllLines(path))
                {
                    var t = line.Trim();
                    if (t.StartsWith("ServerUrl=", StringComparison.OrdinalIgnoreCase))
                        return t.Substring("ServerUrl=".Length).Trim();
                }
            }
            catch
            {
            }
            return DefaultUrl;
        }

        public static void WriteServerUrl(string url, string path = DefaultPath)
        {
            try
            {
                File.WriteAllText(path, $"ServerUrl={url}");
            }
            catch
            {
            }
        }
    }
}
