using System;
using System.IO;
using System.Text.Json;

namespace ScanSnapShot.Models;

public class RemoteServerSettings
{
    public bool Enabled { get; set; } = true;
    public int Port { get; set; } = 50050;
    public string ApiKey { get; set; } = "scansnapshot_secret_key";

    public static string GetFilePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ScanSnapShot",
            "remote_settings.json"
        );
    }

    public static RemoteServerSettings? Load()
    {
        var filePath = GetFilePath();
        if (!File.Exists(filePath))
        {
            // 設定ファイルが存在しない場合は機能無効 (null を返却)
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var settings = JsonSerializer.Deserialize<RemoteServerSettings>(json);
            return settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load remote_settings.json: {ex.Message}");
            return null;
        }
    }

    public static void Save(RemoteServerSettings settings)
    {
        try
        {
            var filePath = GetFilePath();
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save remote_settings.json: {ex.Message}");
        }
    }
}
