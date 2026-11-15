using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace LiquidGlass.WPF.Theme;

public enum GlassThemeMode { Light, Dark, System }

/// <summary>
/// 主题中枢：把 token 里的调色板编译成 WPF 资源字典，控件全部用 DynamicResource 引用，
/// 因此切换主题（或跟随系统）时无需重建任何控件。
/// </summary>
public static class GlassTheme
{
    private const string PaletteKey = "__liquidglass_palette";
    private static readonly List<WeakReference> Hosts = new();

    private static GlassThemeMode _mode = GlassThemeMode.System;
    private static GlassPalette _active = GlassTokens.Current.Light;
    private static bool _reduceTransparency;
    private static bool _increaseContrast;

    /// <summary>主题模式。设置后会立即重写资源字典。</summary>
    public static GlassThemeMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            Refresh();
        }
    }

    /// <summary>辅助功能：降低透明度（iOS「降低透明度」对应实现，玻璃退化为不透明表面）。</summary>
    public static bool ReduceTransparency
    {
        get => _reduceTransparency;
        set { if (_reduceTransparency == value) return; _reduceTransparency = value; Refresh(); }
    }

    /// <summary>辅助功能：增强对比度（描边与投影加深）。</summary>
    public static bool IncreaseContrast
    {
        get => _increaseContrast;
        set { if (_increaseContrast == value) return; _increaseContrast = value; Refresh(); }
    }

    /// <summary>当前生效的调色板。</summary>
    public static GlassPalette Palette => _active;

    /// <summary>当前生效的尺寸/动效 token。</summary>
    public static GlassTokens Tokens => GlassTokens.Current;

    /// <summary>在应用的资源里安装主题（在 App.xaml 的 Startup 或构造阶段调用一次）。</summary>
    public static void Install(Application app)
    {
        if (app is null) throw new ArgumentNullException(nameof(app));
        if (app.Resources.Contains(PaletteKey)) return;
        var dict = BuildDictionary();
        dict[PaletteKey] = true;
        app.Resources.MergedDictionaries.Insert(0, dict);
        if (_mode == GlassThemeMode.System) _active = SystemUsesDark() ? Tokens.Dark : Tokens.Light;
    }

    /// <summary>注册一个会在主题变化时刷新资源引用的宿主元素。</summary>
    public static void Register(FrameworkElement element)
    {
        Hosts.RemoveAll(w => !w.IsAlive);
        Hosts.Add(new WeakReference(element));
    }

    /// <summary>重新生成资源字典并通知所有已注册宿主。</summary>
    public static void Refresh()
    {
        _active = ResolvePalette();
        foreach (var host in Hosts.ToArray())
        {
            if (host.Target is not FrameworkElement fe) continue;
            if (fe is Primitives.GlassSurface surface) surface.Refresh();
            else fe.InvalidateVisual();
            var app = Application.Current;
            if (app is null) continue;
            for (var i = 0; i < app.Resources.MergedDictionaries.Count; i++)
            {
                if (app.Resources.MergedDictionaries[i].Contains(PaletteKey))
                {
                    var fresh = BuildDictionary();
                    fresh[PaletteKey] = true;
                    app.Resources.MergedDictionaries[i] = fresh;
                    break;
                }
            }
        }
    }

    private static GlassPalette ResolvePalette() => _mode switch
    {
        GlassThemeMode.Light => Tokens.Light,
        GlassThemeMode.Dark => Tokens.Dark,
        _ => SystemUsesDark() ? Tokens.Dark : Tokens.Light,
    };

    /// <summary>读取 Windows「个性化 → 颜色 → 应用模式」的浅色/深色设置。</summary>
    public static bool SystemUsesDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var v = key?.GetValue("AppsUseLightTheme");
            return v is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>构造一整套 DynamicResource 键（颜色 + 圆角 + 动效标量）。</summary>
    public static ResourceDictionary BuildDictionary()
    {
        var p = ResolvePalette();
        var t = Tokens;
        var d = new ResourceDictionary();

        // ---- 调色板 ----
        AddColors(d, "Label", p.Label);
        AddColors(d, "LabelSecondary", p.LabelSecondary);
        AddColors(d, "LabelTertiary", p.LabelTertiary);
        AddColors(d, "Accent", p.Accent);
        AddColors(d, "AccentLabel", p.AccentLabel);
        AddColors(d, "Tint", p.Tint);
        AddColors(d, "Surface", p.Surface);
        AddColors(d, "Separator", p.Separator);
        AddColors(d, "SystemFill", p.SystemFill);
        AddColors(d, "Shadow", p.Shadow);
        AddColors(d, "RimLight", p.RimLight);
        AddColors(d, "RimShade", p.RimShade);
        AddColors(d, "Scrim", p.OverlayScrim);

        d["LiquidGlass.Palette.Label"] = new SolidColorBrush(p.Label);
        d["LiquidGlass.Palette.Secondary"] = new SolidColorBrush(p.LabelSecondary);
        d["LiquidGlass.Palette.Tint"] = new SolidColorBrush(p.Tint);
        d["LiquidGlass.Palette.Accent"] = new SolidColorBrush(p.Accent);
        d["LiquidGlass.Palette.Surface"] = new SolidColorBrush(p.Surface);
        d["LiquidGlass.Palette.IsDark"] = IsDark(p);

        // ---- 形状 ----
        d["LiquidGlass.Radius.Control"] = new CornerRadius(t.ShapeControl);
        d["LiquidGlass.Radius.TabBar"] = new CornerRadius(t.ShapeTabBar);
        d["LiquidGlass.Radius.Card"] = new CornerRadius(t.ShapeCard);
        d["LiquidGlass.Radius.Sheet"] = new CornerRadius(t.ShapeSheet);
        d["LiquidGlass.Radius.Window"] = new CornerRadius(t.ShapeWindow);
        d["LiquidGlass.Radius.IconTile"] = new CornerRadius(t.ShapeIconTile);
        d["LiquidGlass.Inset.Concentric"] = t.ShapeConcentricInset;

        // ---- 动效 ----
        d["LiquidGlass.Motion.PressScale"] = t.PressScale;
        d["LiquidGlass.Motion.Stagger"] = t.Stagger;
        d["LiquidGlass.Motion.Snappy.Duration"] = t.SpringSnappy.Duration;
        d["LiquidGlass.Motion.Snappy.Bounce"] = t.SpringSnappy.Bounce;
        d["LiquidGlass.Motion.Smooth.Duration"] = t.SpringSmooth.Duration;
        d["LiquidGlass.Motion.Morph.Duration"] = t.SpringMorph.Duration;
        d["LiquidGlass.Motion.Morph.Bounce"] = t.SpringMorph.Bounce;

        // ---- 字号 ----
        d["LiquidGlass.Font.LargeTitle"] = t.FontLargeTitle;
        d["LiquidGlass.Font.Title"] = t.FontTitle;
        d["LiquidGlass.Font.Body"] = t.FontBody;
        d["LiquidGlass.Font.Footnote"] = t.FontFootnote;
        d["LiquidGlass.Font.Caption"] = t.FontCaption;

        // ---- 辅助功能降级 ----
        d["LiquidGlass.A11y.ReduceTransparency"] = !_reduceTransparency ? 1.0 : 0.0;
        d["LiquidGlass.A11y.BoostContrast"] = _increaseContrast ? 1.0 : 0.0;
        return d;
    }

    private static void AddColors(ResourceDictionary d, string key, Color color)
    {
        d[$"LiquidGlass.Color.{key}"] = color;
        d[$"LiquidGlass.Brush.{key}"] = new SolidColorBrush(color);
    }

    public static bool IsDark(GlassPalette p)
    {
        var lum = 0.2126 * p.Surface.R + 0.7152 * p.Surface.G + 0.0722 * p.Surface.B;
        return lum < 128;
    }
}
