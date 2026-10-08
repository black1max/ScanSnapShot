using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using ScanSnapShot.Models;

namespace ScanSnapShot.Services;

public static class ScreenCaptureService
{
    public static AreaRect GetFullScreenArea()
    {
        return new AreaRect(
            (int)System.Windows.SystemParameters.VirtualScreenLeft,
            (int)System.Windows.SystemParameters.VirtualScreenTop,
            (int)System.Windows.SystemParameters.VirtualScreenWidth,
            (int)System.Windows.SystemParameters.VirtualScreenHeight
        );
    }

    public static Bitmap? CaptureArea(AreaRect area)
    {
        if (!area.IsValid) return null;

        var bmp = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(area.X, area.Y, 0, 0, new Size(area.Width, area.Height), CopyPixelOperation.SourceCopy);
        }
        return bmp;
    }

    public static string SaveBitmap(Bitmap bitmap, string saveDirectory)
    {
        if (!Directory.Exists(saveDirectory))
        {
            Directory.CreateDirectory(saveDirectory);
        }

        var fileName = $"Snap_{DateTime.Now:yyyy_MM_dd_HH_mm_ss_fff}.png";
        var fullPath = Path.Combine(saveDirectory, fileName);
        bitmap.Save(fullPath, ImageFormat.Png);
        return fullPath;
    }

    public static unsafe double CalculateDifferencePercentage(Bitmap bmp1, Bitmap bmp2)
    {
        if (bmp1.Width != bmp2.Width || bmp1.Height != bmp2.Height)
        {
            return 100.0;
        }

        int width = bmp1.Width;
        int height = bmp1.Height;
        long totalPixels = (long)width * height;
        if (totalPixels == 0) return 0.0;

        var rect = new Rectangle(0, 0, width, height);
        var data1 = bmp1.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var data2 = bmp2.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        long diffPixels = 0;
        const int tolerance = 25; // Tolerance for minor rendering/color variations

        try
        {
            byte* scan0_1 = (byte*)data1.Scan0;
            byte* scan0_2 = (byte*)data2.Scan0;
            int stride1 = data1.Stride;
            int stride2 = data2.Stride;

            for (int y = 0; y < height; y++)
            {
                byte* row1 = scan0_1 + (y * stride1);
                byte* row2 = scan0_2 + (y * stride2);

                for (int x = 0; x < width; x++)
                {
                    int offset = x * 4;
                    int diffB = Math.Abs(row1[offset] - row2[offset]);
                    int diffG = Math.Abs(row1[offset + 1] - row2[offset + 1]);
                    int diffR = Math.Abs(row1[offset + 2] - row2[offset + 2]);

                    if (diffB > tolerance || diffG > tolerance || diffR > tolerance)
                    {
                        diffPixels++;
                    }
                }
            }
        }
        finally
        {
            bmp1.UnlockBits(data1);
            bmp2.UnlockBits(data2);
        }

        return (double)diffPixels / totalPixels * 100.0;
    }
}
