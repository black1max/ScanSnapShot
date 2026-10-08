namespace ScanSnapShot.Models;

public class AppSettings
{
    public AreaRect ScanArea { get; set; } = new(100, 100, 300, 200);
    public AreaRect CaptureArea { get; set; } = new(0, 0, 1920, 1080);
    public int IntervalMilliseconds { get; set; } = 1000;
    public double SensitivityThresholdPercent { get; set; } = 3.0; // 3% of pixels changed
    public int CooldownMilliseconds { get; set; } = 1500;
    public string SaveDirectory { get; set; } = string.Empty;
    public bool ShowThumbnail { get; set; } = true;
    public int ThumbnailDurationSeconds { get; set; } = 3;
    public bool IsCaptureFullScreen { get; set; } = false;

    [System.Text.Json.Serialization.JsonIgnore]
    public AreaRect EffectiveCaptureArea => IsCaptureFullScreen
        ? Services.ScreenCaptureService.GetFullScreenArea()
        : CaptureArea;
}
