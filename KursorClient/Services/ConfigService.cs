using System;
using System.IO;
using System.Net;
using System.Text;


namespace KursorClient.Services;

public static class ConfigService
{
    private static readonly object _lock = new object();
    private static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kursor");
    private static readonly string FilePath = Path.Combine(Dir, "config.ini");

    // In-memory representation: simple key -> value (no sections). Keys are case-insensitive.
    private static readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    // Cached parsed endpoint for "server" key (optional)
    public static IPEndPoint? ServerEndpoint { get; private set; }

    static ConfigService()
    {
        Load(); // загрузим при старте
    }

    #region Generic get/set

    public static string GetString(string key, string defaultValue = "")
    {
        lock (_lock)
        {
            if (_values.TryGetValue(key, out var v)) return v;
            return defaultValue;
        }
    }

    public static bool TryGetString(string key, out string? value)
    {
        lock (_lock)
        {
            if (_values.TryGetValue(key, out var v))
            {
                value = v;
                return true;
            }
            value = null;
            return false;
        }
    }

    public static void SetString(string key, string value)
    {
        lock (_lock)
        {
            _values[key] = value ?? "";
            // если ключ server — обновим cached ServerEndpoint
            if (string.Equals(key, "server", StringComparison.OrdinalIgnoreCase))
            {
                TryParseAndSetServer(value);
            }
            Save();
        }
    }

    public static int GetInt(string key, int defaultValue = 0)
    {
        var s = GetString(key, "");
        return int.TryParse(s, out var v) ? v : defaultValue;
    }

    public static void SetInt(string key, int value) => SetString(key, value.ToString());

    public static bool GetBool(string key, bool defaultValue = false)
    {
        var s = GetString(key, "");
        if (bool.TryParse(s, out var b)) return b;
        if (int.TryParse(s, out var i)) return i != 0;
        return defaultValue;
    }

    public static void SetBool(string key, bool value) => SetString(key, value ? "true" : "false");

    #endregion

    #region Server helpers

    // Сохраняет строку и обновляет ServerEndpoint если валидно
    public static void SetServerFromString(string serverString)
    {
        if (string.IsNullOrWhiteSpace(serverString)) throw new ArgumentException("serverString is empty");
        SetString("server", serverString);
        // SetString уже вызовет TryParseAndSetServer и Save
    }

    private static void TryParseAndSetServer(string s)
    {
        // формат host:port
        try
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                ServerEndpoint = null;
                return;
            }
            var parts = s.Split(':');
            if (parts.Length != 2)
            {
                ServerEndpoint = null;
                return;
            }
            if (!int.TryParse(parts[1], out var port))
            {
                ServerEndpoint = null;
                return;
            }
            var addrs = Dns.GetHostAddresses(parts[0]);
            if (addrs.Length == 0)
            {
                ServerEndpoint = null;
                return;
            }
            ServerEndpoint = new IPEndPoint(addrs[0], port);
        }
        catch
        {
            ServerEndpoint = null;
        }
    }

    #endregion

    #region File IO

    private static void EnsureDir() { if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir); }

    private static void Load()
    {
        lock (_lock)
        {
            _values.Clear();
            try
            {
                if (!File.Exists(FilePath)) return;
                var lines = File.ReadAllLines(FilePath, Encoding.UTF8);
                foreach (var raw in lines)
                {
                    var line = raw.Trim();
                    if (line.Length == 0) continue;
                    if (line.StartsWith("#") || line.StartsWith(";")) continue;
                    var idx = line.IndexOf('=');
                    if (idx <= 0) continue;
                    var key = line.Substring(0, idx).Trim();
                    var val = line.Substring(idx + 1).Trim();
                    _values[key] = val;
                }

                // try parse server if present
                if (_values.TryGetValue("server", out var sv)) TryParseAndSetServer(sv);
            }
            catch
            {
                // не фейлим приложение при коррумпированном ini — просто игнорируем
            }
        }
    }

    private static void Save()
    {
        lock (_lock)
        {
            try
            {
                EnsureDir();
                using var sw = new StreamWriter(FilePath, false, Encoding.UTF8);
                sw.WriteLine("# Kursor config.ini");
                foreach (var kv in _values)
                {
                    sw.WriteLine($"{kv.Key}={kv.Value}");
                }
            }
            catch
            {
                // игнорируем ошибки записи
            }
        }
    }

    #endregion
}