namespace ScanSnapShot.Models;

public class AreaRect
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public bool IsValid => Width > 0 && Height > 0;

    public AreaRect() { }

    public AreaRect(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}
