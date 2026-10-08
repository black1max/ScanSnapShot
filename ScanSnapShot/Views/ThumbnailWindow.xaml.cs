using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ScanSnapShot.Views;

public partial class ThumbnailWindow : Window
{
    private readonly string _imagePath;
    private readonly DispatcherTimer _autoCloseTimer;
    private bool _isClosing = false;

    public ThumbnailWindow(string imagePath, int durationSeconds = 3)
    {
        InitializeComponent();

        _imagePath = imagePath;
        TxtFileName.Text = Path.GetFileName(imagePath);

        PositionToBottomRight();
        LoadImage(imagePath);

        int seconds = Math.Max(1, durationSeconds);
        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(seconds)
        };
        _autoCloseTimer.Tick += (s, e) =>
        {
            _autoCloseTimer.Stop();
            StartFadeOut();
        };
        _autoCloseTimer.Start();
    }

    private void PositionToBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        const double margin = 16.0;
        Left = workArea.Right - Width - margin;
        Top = workArea.Bottom - Height - margin;
    }

    private void LoadImage(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var bmp = new BitmapImage();
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = stream;
                    bmp.EndInit();
                }
                bmp.Freeze();
                ImgPreview.Source = bmp;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load thumbnail image: {ex.Message}");
        }
    }

    private void StartFadeOut()
    {
        if (_isClosing) return;
        _isClosing = true;

        var anim = new DoubleAnimation
        {
            From = 1.0,
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(400)
        };
        anim.Completed += (s, e) =>
        {
            Visibility = Visibility.Collapsed;
            Close();
        };
        BeginAnimation(OpacityProperty, anim);
    }

    public void CloseImmediately()
    {
        if (_isClosing) return;
        _isClosing = true;

        _autoCloseTimer.Stop();
        Visibility = Visibility.Collapsed;
        Close();
    }

    private void OnThumbnailClicked(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (File.Exists(_imagePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _imagePath,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open image file: {ex.Message}");
        }

        CloseImmediately();
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        CloseImmediately();
    }
}
