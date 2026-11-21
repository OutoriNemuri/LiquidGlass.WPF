using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LiquidGlass.WPF.Controls;
using LiquidGlass.WPF.Theme;

namespace LiquidGlass.Demo;

public partial class MainWindow : GlassWindow
{
    private static readonly string[] TabTitles = { "图库", "回忆", "收藏", "列表", "设置" };

    public MainWindow()
    {
        InitializeComponent();

        // 右下角的规格说明，直接读 token，保证文字与真实参数一致
        var tokens = GlassTokens.Current;
        var spec = tokens.Material("regular");
        SpecLine.Text =
            $"regular · blur {spec.Blur:0} · lens {spec.LensScale:0.000}\n" +
            $"spring {tokens.SpringSnappy.Duration:0.00}s · bounce {tokens.SpringSnappy.Bounce:0.00} · rim {spec.RimWidth:0}px";

        ThemeToggle.Checked += (_, __) => GlassTheme.Mode = GlassThemeMode.Dark;
        ThemeToggle.Unchecked += (_, __) => GlassTheme.Mode = GlassThemeMode.Light;
    }

    private void OnCloseClick(object sender, MouseButtonEventArgs e) => Close();

    private void OnMinimizeClick(object sender, MouseButtonEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, MouseButtonEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    /// <summary>Dock 点击：移动高亮胶囊并切换标题。</summary>
    private void OnDockClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string tag) return;
        if (!int.TryParse(tag, out var index)) return;

        Grid.SetColumn(DockIndicator, index);
        foreach (var child in GetDockButtons())
            child.Foreground = child == button
                ? Brushes.White
                : new SolidColorBrush(Color.FromArgb(0xB4, 0xFF, 0xFF, 0xFF));

        button.Foreground = Brushes.White;
        Title = $"Liquid Glass · WPF — {TabTitles[Math.Clamp(index, 0, TabTitles.Length - 1)]}";
    }

    private System.Collections.Generic.IEnumerable<Button> GetDockButtons()
    {
        if (DockBar.Content is not Grid grid) yield break;
        foreach (var child in grid.Children)
            if (child is Button b) yield return b;
    }

    /// <summary>渲染质量分段控件点击。</summary>
    private void OnQualityClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBlock text || text.Tag is not string tag) return;
        if (!int.TryParse(tag, out var index)) return;

        Grid.SetColumn(SegThumb, index);
        if (QualitySeg.Content is Grid grid)
        {
            foreach (var child in grid.Children)
            {
                if (child is TextBlock item && item.Tag is string t && int.TryParse(t, out var i))
                {
                    item.FontWeight = i == index ? FontWeights.SemiBold : FontWeights.Normal;
                    item.Opacity = i == index ? 1.0 : 0.72;
                }
            }
        }
    }
}
