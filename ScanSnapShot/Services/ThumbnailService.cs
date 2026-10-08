using System;
using System.IO;
using System.Windows;
using ScanSnapShot.Views;
using Application = System.Windows.Application;

namespace ScanSnapShot.Services;

public class ThumbnailService
{
    private ThumbnailWindow? _currentWindow;

    public void ShowThumbnail(string imagePath, int durationSeconds = 3)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            CloseCurrent();

            try
            {
                if (!File.Exists(imagePath)) return;

                _currentWindow = new ThumbnailWindow(imagePath, durationSeconds);
                _currentWindow.Closed += (s, e) =>
                {
                    if (ReferenceEquals(_currentWindow, s))
                    {
                        _currentWindow = null;
                    }
                };
                _currentWindow.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to display thumbnail: {ex.Message}");
            }
        });
    }

    public void CloseCurrent()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (_currentWindow != null)
            {
                try
                {
                    _currentWindow.CloseImmediately();
                }
                catch { }
                _currentWindow = null;
            }
        });
    }
}
