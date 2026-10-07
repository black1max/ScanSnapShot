using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using ScanSnapShot.Models;

namespace ScanSnapShot.Services;

public class WatcherService
{
    private CancellationTokenSource? _cts;
    private Task? _watchTask;
    private Bitmap? _previousScanBitmap;

    public bool IsRunning { get; private set; }

    public event Action<string>? SnapCaptured;
    public event Action<double>? DiffDetected;
    public event Action<string>? StatusChanged;
    public event Action<string>? ErrorOccurred;

    public void Start(AppSettings settings)
    {
        if (IsRunning) return;

        if (!settings.ScanArea.IsValid)
        {
            ErrorOccurred?.Invoke("ScanArea の範囲が無効です。");
            return;
        }

        if (!settings.CaptureArea.IsValid)
        {
            ErrorOccurred?.Invoke("CaptureArea の範囲が無効です。");
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.SaveDirectory))
        {
            ErrorOccurred?.Invoke("保存フォルダーが指定されていません。");
            return;
        }

        _cts = new CancellationTokenSource();
        IsRunning = true;
        StatusChanged?.Invoke("監視中");

        _watchTask = Task.Run(() => WatchLoopAsync(settings, _cts.Token));
    }

    public async Task StopAsync()
    {
        if (!IsRunning) return;

        _cts?.Cancel();
        if (_watchTask != null)
        {
            try
            {
                await _watchTask;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"監視タスクの終了中にエラー: {ex.Message}");
            }
        }

        _previousScanBitmap?.Dispose();
        _previousScanBitmap = null;

        _cts?.Dispose();
        _cts = null;
        _watchTask = null;
        IsRunning = false;
        StatusChanged?.Invoke("停止中");
    }

    private async Task WatchLoopAsync(AppSettings settings, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Capture ScanArea
                var currentScan = ScreenCaptureService.CaptureArea(settings.ScanArea);
                if (currentScan != null)
                {
                    if (_previousScanBitmap == null)
                    {
                        // Initial reference image
                        _previousScanBitmap = currentScan;
                    }
                    else
                    {
                        double diff = ScreenCaptureService.CalculateDifferencePercentage(_previousScanBitmap, currentScan);
                        DiffDetected?.Invoke(diff);

                        if (diff >= settings.SensitivityThresholdPercent)
                        {
                            // Trigger capture
                            using var captureBitmap = ScreenCaptureService.CaptureArea(settings.CaptureArea);
                            if (captureBitmap != null)
                            {
                                var savedPath = ScreenCaptureService.SaveBitmap(captureBitmap, settings.SaveDirectory);
                                SnapCaptured?.Invoke(savedPath);
                            }

                            // Update reference image to current
                            _previousScanBitmap.Dispose();
                            _previousScanBitmap = currentScan;

                            // Cooldown
                            if (settings.CooldownMilliseconds > 0)
                            {
                                await Task.Delay(settings.CooldownMilliseconds, ct);
                            }
                        }
                        else
                        {
                            // No significant difference, discard current and keep reference
                            currentScan.Dispose();
                        }
                    }
                }

                // Check interval
                int interval = Math.Max(100, settings.IntervalMilliseconds);
                await Task.Delay(interval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"エラー: {ex.Message}");
                await Task.Delay(1000, ct);
            }
        }
    }
}
