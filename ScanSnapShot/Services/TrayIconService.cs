using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScanSnapShot.Services;

public class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _startMenuItem;
    private readonly ToolStripMenuItem _stopMenuItem;

    public event Action? ShowMainWindowRequested;
    public event Action? StartCaptureRequested;
    public event Action? StopCaptureRequested;
    public event Action? ExitRequested;

    public TrayIconService()
    {
        Icon icon = SystemIcons.Application;
        var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
        if (System.IO.File.Exists(iconPath))
        {
            try
            {
                icon = new Icon(iconPath);
            }
            catch { }
        }

        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "ScanSnapShot",
            Visible = false
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("メイン画面を表示", null, (_, _) => ShowMainWindowRequested?.Invoke());

        _startMenuItem = new ToolStripMenuItem("監視・キャプチャーを開始", null, (_, _) => StartCaptureRequested?.Invoke());
        _stopMenuItem = new ToolStripMenuItem("監視・キャプチャーを終了", null, (_, _) => StopCaptureRequested?.Invoke())
        {
            Visible = false
        };

        contextMenu.Items.Add(_startMenuItem);
        contextMenu.Items.Add(_stopMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("アプリケーション終了", null, (_, _) => ExitRequested?.Invoke());

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindowRequested?.Invoke();
    }

    public void UpdateRunningState(bool isRunning)
    {
        _startMenuItem.Visible = !isRunning;
        _stopMenuItem.Visible = isRunning;
        UpdateTooltip(isRunning ? "ScanSnapShot - 監視中" : "ScanSnapShot - 停止中");
    }

    public void Show()
    {
        _notifyIcon.Visible = true;
    }

    public void Hide()
    {
        _notifyIcon.Visible = false;
    }

    public void UpdateTooltip(string text)
    {
        // Max 63 characters for NotifyIcon.Text
        if (text.Length > 63)
        {
            text = text[..60] + "...";
        }
        _notifyIcon.Text = text;
    }

    public void ShowBalloon(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, message, icon);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        GC.SuppressFinalize(this);
    }
}
