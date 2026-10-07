using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScanSnapShot.Models;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ScanSnapShot.Views;

public partial class AreaSelectionWindow : Window
{
    private Point _startPoint;
    private bool _isDragging;
    private AreaRect? _resultArea;

    public AreaRect? SelectedArea => _resultArea;

    public AreaSelectionWindow(string areaName, AreaRect? currentArea = null)
    {
        InitializeComponent();

        TitleText.Text = $"{areaName} をドラッグして選択してください";

        // Virtual Screen (supports multi-monitor setups)
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        if (currentArea != null && currentArea.IsValid)
        {
            // Initial coordinate display
            UpdateSelectionVisual(
                currentArea.X - (int)Left,
                currentArea.Y - (int)Top,
                currentArea.Width,
                currentArea.Height
            );
        }
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            var clickPoint = e.GetPosition(this);

            // すでに選択されたエリアが存在し、ダブルクリックされた場合
            if (e.ClickCount >= 2 && _resultArea != null && _resultArea.IsValid)
            {
                int screenX = (int)clickPoint.X + (int)Left;
                int screenY = (int)clickPoint.Y + (int)Top;

                // 選択範囲の内側をクリックした場合は確定して閉じる
                if (screenX >= _resultArea.X && screenX <= _resultArea.X + _resultArea.Width &&
                    screenY >= _resultArea.Y && screenY <= _resultArea.Y + _resultArea.Height)
                {
                    DialogResult = true;
                    Close();
                    return;
                }
            }

            // 新規ドラッグ選択の開始
            _startPoint = clickPoint;
            _isDragging = true;
            SelectionBorder.Visibility = Visibility.Visible;
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        var currentPoint = e.GetPosition(this);
        var x = (int)Math.Min(_startPoint.X, currentPoint.X);
        var y = (int)Math.Min(_startPoint.Y, currentPoint.Y);
        var width = (int)Math.Abs(currentPoint.X - _startPoint.X);
        var height = (int)Math.Abs(currentPoint.Y - _startPoint.Y);

        UpdateSelectionVisual(x, y, width, height);
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
        }
    }

    private void UpdateSelectionVisual(int localX, int localY, int width, int height)
    {
        Canvas.SetLeft(SelectionBorder, localX);
        Canvas.SetTop(SelectionBorder, localY);
        SelectionBorder.Width = Math.Max(0, width);
        SelectionBorder.Height = Math.Max(0, height);

        int screenX = localX + (int)Left;
        int screenY = localY + (int)Top;

        _resultArea = new AreaRect(screenX, screenY, width, height);
        CoordinateText.Text = $"X: {screenX}, Y: {screenY}, W: {width}, H: {height}";
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
        else if (e.Key == Key.Enter)
        {
            if (_resultArea != null && _resultArea.IsValid)
            {
                DialogResult = true;
                Close();
            }
        }
    }
}
