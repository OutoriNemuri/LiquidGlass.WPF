using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LiquidGlass.WPF.Animation;
using LiquidGlass.WPF.Controls;
using LiquidGlass.WPF.Theme;

namespace LiquidGlass.Demo;

public partial class MainWindow : GlassWindow
{
    private static readonly string[] TabTitles = { "图库", "回忆", "收藏", "列表", "设置" };

    // ── 弹簧驱动：一个 60fps 心跳同时推进所有弹簧，静止后自动摘钩
    private readonly Dictionary<Spring, Action<double>> _drivers = new();
    private readonly Dictionary<FrameworkElement, Spring> _iconSprings = new();
    private readonly Stopwatch _clock = new();
    private readonly Spring _dockSpring = new(0.42, 0.34);
    private readonly Spring _sideSpring = new(0.42, 0.34);
    private bool _ticking;

    private double _dockCell = 98, _sideCell = 44;
    private int _dockIndex, _sideIndex;

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

        Loaded += (_, __) =>
        {
            LayoutDocks(reset: true);
            HookHover();
        };
        SizeChanged += (_, __) => LayoutDocks(reset: false);
    }

    // ── 弹簧心跳 ────────────────────────────────────────────────
    private void Drive(Spring spring, Action<double> apply)
    {
        _drivers[spring] = apply;
        if (_ticking) return;
        _ticking = true;
        _clock.Restart();
        CompositionTarget.Rendering += OnFrame;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var dt = Math.Min(0.05, _clock.Elapsed.TotalSeconds);
        _clock.Restart();

        var alive = false;
        foreach (var pair in new List<KeyValuePair<Spring, Action<double>>>(_drivers))
        {
            var spring = pair.Key;
            // 注意：不能用 IsSettled 做前置判断——刚改过 Target 的弹簧 IsSettled 仍为 true，
            // 会被直接摘钩、永远不动。交给 Step 自己判断（它内部会跳过真正静止的弹簧）。
            spring.Step(dt);
            pair.Value(spring.Value);
            if (spring.IsSettled) _drivers.Remove(spring);
            else alive = true;
        }

        if (!alive)
        {
            CompositionTarget.Rendering -= OnFrame;
            _ticking = false;
        }
    }

    // ── Dock 布局与高亮 ─────────────────────────────────────────
    private static Grid? ContentGrid(ContentControl host) => host.Content as Grid;

    private void LayoutDocks(bool reset)
    {
        if (ContentGrid(DockBar) is { ActualWidth: > 0 } dockGrid)
        {
            _dockCell = dockGrid.ActualWidth / 5.0;
            DockIndicator.Width = Math.Max(12, _dockCell - 6);
            if (reset) { _dockSpring.Target = 0; DockShift.X = 0; }
            else MoveDockIndicator(_dockIndex, animate: false);
        }

        if (ContentGrid(SideDock) is { ActualHeight: > 0 } sideGrid)
        {
            _sideCell = sideGrid.ActualHeight / 5.0;
            SideIndicator.Height = Math.Max(12, _sideCell - 8);
            if (reset) { _sideSpring.Target = 0; SideShift.Y = 0; }
            else MoveSideIndicator(_sideIndex, animate: false);
        }
    }

    /// <summary>
    /// 高亮胶囊用弹簧位移，并按速度做沿运动方向的拉伸（squash &amp; stretch）。
    /// 这是 iOS 标签栏在内容切换时的"液态"手感来源。
    /// </summary>
    private void MoveDockIndicator(int index, bool animate)
    {
        _dockIndex = index;
        var target = index * _dockCell;

        if (!animate)
        {
            _dockSpring.Retarget(target);
            DockShift.X = target;
            DockSquash.ScaleX = 1;
            DockSquash.ScaleY = 1;
            return;
        }

        _dockSpring.Retarget(target);
        Drive(_dockSpring, v =>
        {
            DockShift.X = v;
            var stretch = Math.Min(0.22, Math.Abs(_dockSpring.Velocity) * 0.035);
            DockSquash.ScaleX = 1 + stretch;
            DockSquash.ScaleY = 1 - stretch * 0.55;
        });
    }

    private void MoveSideIndicator(int index, bool animate)
    {
        _sideIndex = index;
        var target = index * _sideCell;

        if (!animate)
        {
            _sideSpring.Retarget(target);
            SideShift.Y = target;
            SideSquash.ScaleX = 1;
            SideSquash.ScaleY = 1;
            return;
        }

        _sideSpring.Retarget(target);
        Drive(_sideSpring, v =>
        {
            SideShift.Y = v;
            var stretch = Math.Min(0.24, Math.Abs(_sideSpring.Velocity) * 0.030);
            SideSquash.ScaleY = 1 + stretch;
            SideSquash.ScaleX = 1 - stretch * 0.55;
        });
    }

    /// <summary>点击时图标做一次弹簧回弹（按下 0.86 → 松开弹出）。</summary>
    private void PopIcon(Button button)
    {
        var icon = FindIcon(button);
        if (icon is null) return;

        if (icon.RenderTransform is not ScaleTransform scale)
        {
            scale = new ScaleTransform(1, 1);
            icon.RenderTransformOrigin = new Point(0.5, 0.5);
            icon.RenderTransform = scale;
        }

        if (!_iconSprings.TryGetValue(icon, out var spring))
        {
            spring = new Spring(0.45, 0.55, 1.0);
            _iconSprings[icon] = spring;
        }

        spring.Target = 1.0;
        spring.Poke(7.0);
        Drive(spring, v =>
        {
            scale.ScaleX = v;
            scale.ScaleY = v;
        });
    }

    private static FrameworkElement? FindIcon(Button button) => button.Content switch
    {
        Panel { Children.Count: > 0 } panel => panel.Children[0] as FrameworkElement,
        FrameworkElement fe => fe,
        _ => null,
    };

    private void HookHover()
    {
        foreach (var button in EnumerateButtons(DockBar)) Hook(button, 1.10);
        foreach (var button in EnumerateButtons(SideDock)) Hook(button, 1.16);

        void Hook(Button button, double hover)
        {
            button.MouseEnter += (_, __) => HoverIcon(button, hover);
            button.MouseLeave += (_, __) => HoverIcon(button, 1.0);
        }
    }

    private void HoverIcon(Button button, double target)
    {
        var icon = FindIcon(button);
        if (icon is null) return;
        if (icon.RenderTransform is not ScaleTransform scale)
        {
            scale = new ScaleTransform(1, 1);
            icon.RenderTransformOrigin = new Point(0.5, 0.5);
            icon.RenderTransform = scale;
        }
        if (!_iconSprings.TryGetValue(icon, out var spring))
        {
            spring = new Spring(0.34, 0.30, 1.0);
            _iconSprings[icon] = spring;
        }
        spring.Target = target;
        Drive(spring, v =>
        {
            scale.ScaleX = v;
            scale.ScaleY = v;
        });
    }

    private static IEnumerable<Button> EnumerateButtons(ContentControl host)
    {
        if (host.Content is not Panel panel) yield break;
        foreach (var child in panel.Children)
            if (child is Button b) yield return b;
    }

    private void OnCloseClick(object sender, MouseButtonEventArgs e) => Close();

    private void OnMinimizeClick(object sender, MouseButtonEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, MouseButtonEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    /// <summary>底部 Dock 点击：弹簧移动高亮胶囊、图标回弹、联动竖向 Dock。</summary>
    private void OnDockClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string tag) return;
        if (!int.TryParse(tag, out var index)) return;

        MoveDockIndicator(index, animate: true);
        PopIcon(button);
        MoveSideIndicator(index, animate: true);
        Highlight(DockBar, index);
        Highlight(SideDock, index);
        Title = $"Liquid Glass · WPF — {TabTitles[Math.Clamp(index, 0, TabTitles.Length - 1)]}";
    }

    /// <summary>竖向 Dock 点击：弹簧移动胶囊并联动底部 Dock。</summary>
    private void OnSideDockClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string tag) return;
        if (!int.TryParse(tag, out var index)) return;

        MoveSideIndicator(index, animate: true);
        PopIcon(button);
        MoveDockIndicator(index, animate: true);
        Highlight(SideDock, index);
        Highlight(DockBar, index);
        Title = $"Liquid Glass · WPF — {TabTitles[Math.Clamp(index, 0, TabTitles.Length - 1)]}";
    }

    private static void Highlight(ContentControl host, int index)
    {
        var i = 0;
        foreach (var button in EnumerateButtons(host))
        {
            button.Foreground = i == index
                ? Brushes.White
                : new SolidColorBrush(Color.FromArgb(0xB4, 0xFF, 0xFF, 0xFF));
            i++;
        }
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
