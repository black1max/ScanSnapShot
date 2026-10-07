using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using ScanSnapShot.Models;
using ScanSnapShot.Services;
using ScanSnapShot.Views;
using MessageBox = System.Windows.MessageBox;

namespace ScanSnapShot;

public partial class MainWindow : Window
{
    private AppSettings _settings;
    private readonly WatcherService _watcherService;
    private readonly TrayIconService _trayIconService;
    private bool _isExplicitExit = false;

    public MainWindow()
    {
        InitializeComponent();

        LogService.Initialize();
        _settings = SettingsService.Load();
        _watcherService = new WatcherService();
        _trayIconService = new TrayIconService();

        SetupEventHandlers();
        ApplySettingsToUI();
        AddLog("アプリケーションを起動しました。設定をロードしました。");
    }

    private void SetupEventHandlers()
    {
        _watcherService.SnapCaptured += (path) =>
        {
            Dispatcher.Invoke(() =>
            {
                AddLog($"[撮影保存] {Path.GetFileName(path)}");
                _trayIconService.ShowBalloon("キャプチャー完了", $"画像を保存しました: {Path.GetFileName(path)}");
            });
        };

        _watcherService.DiffDetected += (diff) =>
        {
            Dispatcher.Invoke(() =>
            {
                TxtStatus.Text = $"監視中 - 前回からの変化: {diff:F2}% (閾値: {_settings.SensitivityThresholdPercent:F1}%)";
            });
        };

        _watcherService.StatusChanged += (status) =>
        {
            Dispatcher.Invoke(() =>
            {
                TxtStatus.Text = status;
                UpdateUiForState(_watcherService.IsRunning);
            });
        };

        _watcherService.ErrorOccurred += (error) =>
        {
            Dispatcher.Invoke(() =>
            {
                AddLog($"[エラー] {error}");
                MessageBox.Show(this, error, "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        };

        _trayIconService.ShowMainWindowRequested += () =>
        {
            Dispatcher.Invoke(() =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            });
        };

        _trayIconService.StartCaptureRequested += () =>
        {
            Dispatcher.Invoke(() =>
            {
                if (!_watcherService.IsRunning)
                {
                    StartMonitoringAndMinimize();
                }
            });
        };

        _trayIconService.StopCaptureRequested += async () =>
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                await _watcherService.StopAsync();
                Show();
                WindowState = WindowState.Normal;
                Activate();
                AddLog("タスクトレイメニューから監視を終了しました。");
            });
        };

        _trayIconService.ExitRequested += () =>
        {
            Dispatcher.Invoke(() =>
            {
                _isExplicitExit = true;
                Close();
            });
        };
    }

    private void ApplySettingsToUI()
    {
        UpdateAreaLabels();
        TxtInterval.Text = _settings.IntervalMilliseconds.ToString();
        TxtSensitivity.Text = _settings.SensitivityThresholdPercent.ToString("F1");
        TxtCooldown.Text = _settings.CooldownMilliseconds.ToString();
        TxtSaveDirectory.Text = _settings.SaveDirectory;
    }

    private void UpdateAreaLabels()
    {
        var s = _settings.ScanArea;
        TxtScanAreaInfo.Text = $"X: {s.X}, Y: {s.Y}, 幅: {s.Width}, 高さ: {s.Height}";

        var c = _settings.CaptureArea;
        TxtCaptureAreaInfo.Text = $"X: {c.X}, Y: {c.Y}, 幅: {c.Width}, 高さ: {c.Height}";
    }

    private bool ValidateAndApplySettings(bool showDialogOnError = true)
    {
        // 1. チェック間隔
        if (!int.TryParse(TxtInterval.Text, out var interval) || interval < 50 || interval > 300_000)
        {
            if (showDialogOnError)
            {
                MessageBox.Show(
                    "「チェック間隔」は 50 〜 300,000 ms (0.05秒〜5分) の範囲内の整数を入力してください。\n(推奨: 500 〜 1000 ms)",
                    "入力エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                TxtInterval.Focus();
                TxtInterval.SelectAll();
            }
            return false;
        }

        // 2. 変化しきい値
        if (!double.TryParse(TxtSensitivity.Text, out var sens) || sens < 0.1 || sens > 100.0)
        {
            if (showDialogOnError)
            {
                MessageBox.Show(
                    "「変化しきい値」は 0.1 〜 100.0 % の範囲内の数値を入力してください。\n(推奨: 1.0 〜 5.0 %)",
                    "入力エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                TxtSensitivity.Focus();
                TxtSensitivity.SelectAll();
            }
            return false;
        }

        // 3. クールダウン
        if (!int.TryParse(TxtCooldown.Text, out var cd) || cd < 0 || cd > 300_000)
        {
            if (showDialogOnError)
            {
                MessageBox.Show(
                    "「クールダウン」は 0 〜 300,000 ms (0秒〜5分) の範囲内の整数を入力してください。\n(推奨: 1000 〜 2000 ms)",
                    "入力エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                TxtCooldown.Focus();
                TxtCooldown.SelectAll();
            }
            return false;
        }

        _settings.IntervalMilliseconds = interval;
        _settings.SensitivityThresholdPercent = sens;
        _settings.CooldownMilliseconds = cd;
        _settings.SaveDirectory = TxtSaveDirectory.Text;

        SettingsService.Save(_settings);
        return true;
    }

    private void OnSelectScanAreaClicked(object sender, RoutedEventArgs e)
    {
        var selectionWindow = new AreaSelectionWindow("ScanArea (監視エリア)", _settings.ScanArea);
        if (selectionWindow.ShowDialog() == true && selectionWindow.SelectedArea != null)
        {
            _settings.ScanArea = selectionWindow.SelectedArea;
            UpdateAreaLabels();
            SettingsService.Save(_settings);
            AddLog($"ScanArea を更新: X={_settings.ScanArea.X}, Y={_settings.ScanArea.Y}, W={_settings.ScanArea.Width}, H={_settings.ScanArea.Height}");
        }
    }

    private void OnSelectCaptureAreaClicked(object sender, RoutedEventArgs e)
    {
        var selectionWindow = new AreaSelectionWindow("CaptureArea (撮影エリア)", _settings.CaptureArea);
        if (selectionWindow.ShowDialog() == true && selectionWindow.SelectedArea != null)
        {
            _settings.CaptureArea = selectionWindow.SelectedArea;
            UpdateAreaLabels();
            SettingsService.Save(_settings);
            AddLog($"CaptureArea を更新: X={_settings.CaptureArea.X}, Y={_settings.CaptureArea.Y}, W={_settings.CaptureArea.Width}, H={_settings.CaptureArea.Height}");
        }
    }

    private void OnSyncCaptureAreaClicked(object sender, RoutedEventArgs e)
    {
        _settings.CaptureArea = new AreaRect(
            _settings.ScanArea.X,
            _settings.ScanArea.Y,
            _settings.ScanArea.Width,
            _settings.ScanArea.Height
        );
        UpdateAreaLabels();
        SettingsService.Save(_settings);
        AddLog("CaptureArea を ScanArea と同じ範囲に設定しました。");
    }

    private void OnBrowseFolderClicked(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "キャプチャー画像の保存先フォルダーを選択してください",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_settings.SaveDirectory) ? _settings.SaveDirectory : ""
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _settings.SaveDirectory = dialog.SelectedPath;
            TxtSaveDirectory.Text = _settings.SaveDirectory;
            SettingsService.Save(_settings);
            AddLog($"保存先フォルダーを変更: {_settings.SaveDirectory}");
        }
    }

    private void OnOpenFolderClicked(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(_settings.SaveDirectory))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _settings.SaveDirectory,
                UseShellExecute = true
            });
        }
        else
        {
            MessageBox.Show("フォルダーが存在しません。", "確認", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnTestCaptureClicked(object sender, RoutedEventArgs e)
    {
        if (!ValidateAndApplySettings(showDialogOnError: true)) return;

        using var bmp = ScreenCaptureService.CaptureArea(_settings.CaptureArea);
        if (bmp != null)
        {
            var path = ScreenCaptureService.SaveBitmap(bmp, _settings.SaveDirectory);
            AddLog($"[テスト撮影成功] {path}");
            MessageBox.Show($"CaptureArea のテスト撮影に成功しました:\n{path}", "テスト成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("CaptureArea の指定が無効です。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnStartStopClicked(object sender, RoutedEventArgs e)
    {
        if (_watcherService.IsRunning)
        {
            await _watcherService.StopAsync();
            UpdateUiForState(false);
        }
        else
        {
            StartMonitoringAndMinimize();
        }
    }

    private void StartMonitoringAndMinimize()
    {
        if (!ValidateAndApplySettings(showDialogOnError: true)) return;

        _watcherService.Start(_settings);
        if (_watcherService.IsRunning)
        {
            UpdateUiForState(true);

            AddLog("==================== 監視開始 ====================");
            AddLog($"[監視エリア (ScanArea)] X={_settings.ScanArea.X}, Y={_settings.ScanArea.Y}, 幅={_settings.ScanArea.Width}, 高さ={_settings.ScanArea.Height}");
            AddLog($"[キャプチャーエリア (CaptureArea)] X={_settings.CaptureArea.X}, Y={_settings.CaptureArea.Y}, 幅={_settings.CaptureArea.Width}, 高さ={_settings.CaptureArea.Height}");
            AddLog($"[監視パラメータ] 間隔={_settings.IntervalMilliseconds}ms, しきい値={_settings.SensitivityThresholdPercent:F1}%, クールダウン={_settings.CooldownMilliseconds}ms");
            AddLog($"[保存先フォルダー] {_settings.SaveDirectory}");
            AddLog("タスクトレイに格納してバックグラウンド監視を開始しました。");
            AddLog("==================================================");

            // Minimize to system tray
            _trayIconService.Show();
            Hide();
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        // 最小化ボタンを押されたらタスクトレイに格納してウィンドウを非表示にする
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            _trayIconService.Show();
            _trayIconService.UpdateRunningState(_watcherService.IsRunning);
        }
    }

    private void UpdateUiForState(bool isRunning)
    {
        _trayIconService.UpdateRunningState(isRunning);

        if (isRunning)
        {
            BtnStartStop.Content = "監視停止";
            BtnStartStop.Background = System.Windows.Media.Brushes.Crimson;
            _trayIconService.Show();
        }
        else
        {
            BtnStartStop.Content = "監視開始（タスクトレイへ格納）";
            BtnStartStop.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#0078D7")!;
            TxtStatus.Text = "待機中";
        }
    }

    private void AddLog(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        TxtLog.AppendText(line + Environment.NewLine);
        LogScrollViewer.ScrollToEnd();

        // NLog への保存
        if (msg.StartsWith("[エラー]"))
        {
            LogService.Error(msg);
        }
        else
        {
            LogService.Info(msg);
        }
    }

    private async void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_watcherService.IsRunning && !_isExplicitExit)
        {
            // If user clicks the X button while monitoring, minimize to tray instead
            e.Cancel = true;
            Hide();
            _trayIconService.Show();
            _trayIconService.ShowBalloon("バックグラウンド実行中", "監視を継続しています。トレイアイコンの右クリックから終了できます。");
            return;
        }

        ValidateAndApplySettings(showDialogOnError: false);
        if (_watcherService.IsRunning)
        {
            await _watcherService.StopAsync();
        }
        _trayIconService.Dispose();
    }
}