using System;
using System.IO;
using NLog;
using NLog.Config;

namespace ScanSnapShot.Services;

public static class LogService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static void Initialize()
    {
        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NLog.config");
        if (File.Exists(configPath))
        {
            // NLog.config（ログ出力先、日別ローテーション、30日自動削除など）から設定を読み込み
            LogManager.Configuration = new XmlLoggingConfiguration(configPath);
        }
    }

    public static void Info(string message) => Logger.Info(message);
    public static void Warn(string message) => Logger.Warn(message);
    public static void Error(string message, Exception? ex = null)
    {
        if (ex != null)
            Logger.Error(ex, message);
        else
            Logger.Error(message);
    }
}
