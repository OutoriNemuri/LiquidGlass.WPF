using System;
using System.Windows;
using System.Windows.Controls;
using LiquidGlass.WPF.Controls;
using LiquidGlass.WPF.Theme;

namespace LiquidGlass.Demo;

public partial class MainWindow : GlassWindow
{
    public MainWindow()
    {
        InitializeComponent();

        // 分段控件的选项（也可以直接在 XAML 里用 Items 绑定 ViewModel）
        QualitySeg.Items = new[] { "标准", "高保真", "无损" };

        // 右下角的规格说明，直接读 token，保证文字与真实参数一致
        var tokens = GlassTokens.Current;
        var spec = tokens.Material("regular");
        SpecLine.Text =
            $"regular · blur {spec.Blur:0} · lens {spec.LensScale:0.000}\n" +
            $"spring {tokens.SpringSnappy.Duration:0.00}s · bounce {tokens.SpringSnappy.Bounce:0.00} · rim {spec.RimWidth:0}px";

        // 深色外观开关：切换整套 token 调色板（所有玻璃会立刻重新合成）
        ThemeToggle.Checked += (_, __) => GlassTheme.Mode = GlassThemeMode.Dark;
        ThemeToggle.Unchecked += (_, __) => GlassTheme.Mode = GlassThemeMode.Light;

        // 标签栏切换时更新标题
        TabBar.SelectionChanged += OnTabChanged;

        Loaded += (_, __) =>
        {
            // 静态场景不需要实时捕获；滚动/视频场景把它设成 true
            BackdropLayer.LayoutUpdated += (_, __) => { };
        };
    }

    private void OnTabChanged(object? sender, EventArgs e)
    {
        var index = TabBar.SelectedIndex;
        var titles = new[] { "图库", "回忆", "收藏", "列表", "设置" };
        Title = $"Liquid Glass · WPF — {titles[Math.Clamp(index, 0, titles.Length - 1)]}";
    }
}
