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
    private readonly ThumbnailService _thumbnailService;
    private readonly RemoteServerService _remoteServerService;
    private bool _isExplicitExit = false;

    public MainWindow()
    {
        InitializeComponent();

        LogService.Initialize();
        _settings = SettingsService.Load();
        _watcherService = new WatcherService();
        _trayIconService = new TrayIconService();
        _thumbnailService = new ThumbnailService();
        _remoteServerService = new RemoteServerService();

        SetupEventHandlers();
        ApplySettingsToUI();
        AddLog("アプリケーションを起動しました。設定をロードしました。");
        LogCurrentSettings("現在の設定値");
        SetupRemoteServer();
    }

    private void SetupEventHandlers()
    {
        _watcherService.BeforeCapture += () =>
        {
            Dispatcher.Invoke(() =>
            {
                _thumbnailService.CloseCurrent();
            });
        };

        _watcherService.SnapCaptured += (path) =>
        {
            Dispatcher.Invoke(() =>
            {
                AddLog($"[撮影保存] {Path.GetFileName(path)}");
                _trayIconService.ShowBalloon("キャプチャー完了", $"画像を保存しました: {Path.GetFileName(path)}");
                if (_settings.ShowThumbnail)
                {
                    _thumbnailService.ShowThumbnail(path, _settings.ThumbnailDurationSeconds);
                }
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

        _trayIconService.CaptureRequested += async () =>
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                // メニューが完全に閉じるのを少し待機して写り込みを防止
                await Task.Delay(200);
                await ExecuteCaptureAsync("トレイキャプチャー");
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
        if (_settings.IsCaptureFullScreen)
        {
            RbCaptureFullScreen.IsChecked = true;
        }
        else
        {
            RbCaptureCustom.IsChecked = true;
        }

        UpdateAreaLabels();
        TxtInterval.Text = _settings.IntervalMilliseconds.ToString();
        TxtSensitivity.Text = _settings.SensitivityThresholdPercent.ToString("F1");
        TxtCooldown.Text = _settings.CooldownMilliseconds.ToString();
        TxtSaveDirectory.Text = _settings.SaveDirectory;
        ChkShowThumbnail.IsChecked = _settings.ShowThumbnail;
        TxtThumbnailDuration.Text = _settings.ThumbnailDurationSeconds.ToString();
        TxtThumbnailDuration.IsEnabled = _settings.ShowThumbnail;
    }

    private void OnShowThumbnailCheckedChanged(object sender, RoutedEventArgs e)
    {
        if (TxtThumbnailDuration != null)
        {
            TxtThumbnailDuration.IsEnabled = ChkShowThumbnail.IsChecked == true;
        }
    }

    private void UpdateAreaLabels()
    {
        var s = _settings.ScanArea;
        TxtScanAreaInfo.Text = $"X: {s.X}, Y: {s.Y}, 幅: {s.Width}, 高さ: {s.Height}";

        if (_settings.IsCaptureFullScreen)
        {
            var full = ScreenCaptureService.GetFullScreenArea();
            TxtCaptureAreaInfo.Text = $"全画面 (X: {full.X}, Y: {full.Y}, 幅: {full.Width}, 高さ: {full.Height})";
            PanelCaptureCustomButtons.IsEnabled = false;
            PanelCaptureCustomButtons.Opacity = 0.5;
        }
        else
        {
            var c = _settings.CaptureArea;
            TxtCaptureAreaInfo.Text = $"X: {c.X}, Y: {c.Y}, 幅: {c.Width}, 高さ: {c.Height}";
            PanelCaptureCustomButtons.IsEnabled = true;
            PanelCaptureCustomButtons.Opacity = 1.0;
        }
    }

    private void OnCaptureAreaModeChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _settings.IsCaptureFullScreen = RbCaptureFullScreen.IsChecked == true;
        UpdateAreaLabels();
        SettingsService.Save(_settings);
        AddLog($"キャプチャーエリアモードを変更: {(_settings.IsCaptureFullScreen ? "全画面" : "指定範囲")}");
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

        // 4. サムネイル表示時間
        if (!int.TryParse(TxtThumbnailDuration.Text, out var duration) || duration < 1 || duration > 60)
        {
            if (showDialogOnError)
            {
                MessageBox.Show(
                    "「サムネイル表示時間」は 1 〜 60 秒の範囲内の整数を入力してください。\n(推奨: 1 〜 10 秒, デフォルト: 3 秒)",
                    "入力エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                TxtThumbnailDuration.Focus();
                TxtThumbnailDuration.SelectAll();
            }
            return false;
        }

        _settings.IntervalMilliseconds = interval;
        _settings.SensitivityThresholdPercent = sens;
        _settings.CooldownMilliseconds = cd;
        _settings.SaveDirectory = TxtSaveDirectory.Text;
        _settings.ShowThumbnail = ChkShowThumbnail.IsChecked == true;
        _settings.ThumbnailDurationSeconds = duration;
        _settings.IsCaptureFullScreen = RbCaptureFullScreen.IsChecked == true;

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

    private async Task<string?> ExecuteCaptureAsync(string logPrefix = "手動キャプチャー")
    {
        if (!ValidateAndApplySettings(showDialogOnError: false)) return null;

        _thumbnailService.CloseCurrent();

        var area = _settings.EffectiveCaptureArea;
        if (!area.IsValid)
        {
            AddLog("[エラー] キャプチャーエリアの範囲が無効です。");
            return null;
        }

        if (string.IsNullOrWhiteSpace(_settings.SaveDirectory))
        {
            AddLog("[エラー] 保存先フォルダーが指定されていません。");
            return null;
        }

        using var bmp = ScreenCaptureService.CaptureArea(area);
        if (bmp != null)
        {
            var path = ScreenCaptureService.SaveBitmap(bmp, _settings.SaveDirectory);
            AddLog($"[{logPrefix}] {Path.GetFileName(path)}");
            _trayIconService.ShowBalloon("キャプチャー完了", $"画像を保存しました: {Path.GetFileName(path)}");
            if (_settings.ShowThumbnail)
            {
                _thumbnailService.ShowThumbnail(path, _settings.ThumbnailDurationSeconds);
            }
            return path;
        }
        else
        {
            AddLog("[エラー] 画面キャプチャーに失敗しました。");
            return null;
        }
    }

    private async void OnTestCaptureClicked(object sender, RoutedEventArgs e)
    {
        if (!ValidateAndApplySettings(showDialogOnError: true)) return;

        var path = await ExecuteCaptureAsync("テスト撮影成功");
        if (!string.IsNullOrEmpty(path))
        {
            var modeText = _settings.IsCaptureFullScreen ? "全画面" : "CaptureArea";
            MessageBox.Show($"{modeText} のテスト撮影に成功しました:\n{path}", "テスト成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("画面キャプチャーに失敗しました。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnStartStopClicked(object sender, RoutedEventArgs e)
    {
        if (_watcherService.IsRunning)
        {
            _thumbnailService.CloseCurrent();
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

            LogCurrentSettings("監視開始");
            AddLog("タスクトレイに格納してバックグラウンド監視を開始しました。");

            // Minimize to system tray
            _trayIconService.Show();
            Hide();
        }
    }

    private void LogCurrentSettings(string header = "現在の設定値")
    {
        var capArea = _settings.EffectiveCaptureArea;
        var capInfo = _settings.IsCaptureFullScreen
            ? $"全画面 (X={capArea.X}, Y={capArea.Y}, 幅={capArea.Width}, 高さ={capArea.Height})"
            : $"指定範囲 (X={capArea.X}, Y={capArea.Y}, 幅={capArea.Width}, 高さ={capArea.Height})";
        var thumbInfo = _settings.ShowThumbnail ? $"{_settings.ThumbnailDurationSeconds}秒" : "OFF";

        AddLog($"==================== {header} ====================");
        AddLog($"[監視エリア (ScanArea)] X={_settings.ScanArea.X}, Y={_settings.ScanArea.Y}, 幅={_settings.ScanArea.Width}, 高さ={_settings.ScanArea.Height}");
        AddLog($"[キャプチャーエリア (CaptureArea)] {capInfo}");
        AddLog($"[監視パラメータ] 間隔={_settings.IntervalMilliseconds}ms, しきい値={_settings.SensitivityThresholdPercent:F1}%, クールダウン={_settings.CooldownMilliseconds}ms, サムネイル={thumbInfo}");
        AddLog($"[保存先フォルダー] {_settings.SaveDirectory}");
        AddLog("==================================================");
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

        _thumbnailService.CloseCurrent();
        ValidateAndApplySettings(showDialogOnError: false);
        if (_watcherService.IsRunning)
        {
            await _watcherService.StopAsync();
        }
        await _remoteServerService.StopAsync();
        _trayIconService.Dispose();
    }

    private void SetupRemoteServer()
    {
        _remoteServerService.CaptureHandler = async () =>
        {
            return await Dispatcher.Invoke(() => ExecuteCaptureAsync("リモートキャプチャー"));
        };

        _remoteServerService.SettingsUpdateHandler = (newSettings) =>
        {
            var isValid = Dispatcher.Invoke(() =>
            {
                _settings = newSettings;
                ApplySettingsToUI();
                var valid = ValidateAndApplySettings(showDialogOnError: false);
                if (valid)
                {
                    AddLog("リモートAPI経由で設定を更新・保存しました。");
                    LogCurrentSettings("更新後の設定値");
                }
                return valid;
            });
            return Task.FromResult(isValid);
        };

        _remoteServerService.StartMonitoringHandler = () =>
        {
            var isRunning = Dispatcher.Invoke(() =>
            {
                if (_watcherService.IsRunning) return true;
                StartMonitoringAndMinimize();
                return _watcherService.IsRunning;
            });
            return Task.FromResult(isRunning);
        };

        _remoteServerService.StopMonitoringHandler = async () =>
        {
            return await Dispatcher.Invoke(async () =>
            {
                if (!_watcherService.IsRunning) return true;
                _thumbnailService.CloseCurrent();
                await _watcherService.StopAsync();
                UpdateUiForState(false);
                AddLog("リモートAPI経由で監視を停止しました。");
                return true;
            });
        };

        _remoteServerService.StatusProvider = () =>
        {
            return Dispatcher.Invoke(() => new
            {
                isRunning = _watcherService.IsRunning,
                isCaptureFullScreen = _settings.IsCaptureFullScreen,
                scanArea = _settings.ScanArea,
                captureArea = _settings.EffectiveCaptureArea,
                intervalMilliseconds = _settings.IntervalMilliseconds,
                sensitivityThresholdPercent = _settings.SensitivityThresholdPercent,
                cooldownMilliseconds = _settings.CooldownMilliseconds,
                saveDirectory = _settings.SaveDirectory
            });
        };

        _remoteServerService.SettingsProvider = () => _settings;

        _remoteServerService.LogAction = (msg) =>
        {
            Dispatcher.Invoke(() => AddLog(msg));
        };

        // バックグラウンドでサーバーを起動 (remote_settings.json が存在する場合のみ有効)
        _ = _remoteServerService.StartAsync();
    }
}