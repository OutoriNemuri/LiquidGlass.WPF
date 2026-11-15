using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows.Media;

namespace LiquidGlass.WPF.Theme;

/// <summary>一个材质的光学参数（对应 tokens -> material.*）。</summary>
public sealed class GlassMaterialSpec
{
    public string Name { get; init; } = "regular";
    public double Blur { get; init; } = 24;
    public double Saturation { get; init; } = 1.72;
    public double Brightness { get; init; } = 1.04;
    public double LensScale { get; init; } = 1.035;
    public double LensOpacity { get; init; } = 0.45;
    public double LensBlur { get; init; } = 10;
    public double Dispersion { get; init; } = 0.16;
    public double Specular { get; init; } = 0.30;
    public double Noise { get; init; } = 0.045;
    public double RimWidth { get; init; } = 1;
    public double ShadowOpacity { get; init; } = 1.0;
    public double ShadowY { get; init; } = 22;
    public double ShadowBlur { get; init; } = 46;
    public double InnerGlow { get; init; } = 0.22;
    public double TintOpacity { get; init; } = 0.50;
}

/// <summary>一套调色板（light / dark）。</summary>
public sealed class GlassPalette
{
    public Color Tint { get; init; } = Colors.White;
    public double TintOpacity { get; init; } = 0.34;
    public double TintOpacityClear { get; init; } = 0.14;
    public Color Label { get; init; } = Color.FromRgb(0x10, 0x13, 0x18);
    public Color LabelSecondary { get; init; } = Color.FromArgb(0x94, 0x10, 0x13, 0x18);
    public Color LabelTertiary { get; init; } = Color.FromArgb(0x61, 0x10, 0x13, 0x18);
    public Color Accent { get; init; } = Color.FromRgb(0x0A, 0x7C, 0xFF);
    public Color AccentLabel { get; init; } = Colors.White;
    public Color SystemFill { get; init; } = Color.FromArgb(0x24, 0x78, 0x82, 0x96);
    public Color Separator { get; init; } = Color.FromArgb(0x1A, 0x10, 0x13, 0x18);
    public Color Surface { get; init; } = Color.FromRgb(0xF3, 0xF4, 0xF7);
    public Color RimLight { get; init; } = Color.FromArgb(0xB8, 0xFF, 0xFF, 0xFF);
    public Color RimShade { get; init; } = Color.FromArgb(0x1A, 0x0C, 0x12, 0x20);
    public Color Shadow { get; init; } = Color.FromArgb(0x38, 0x0E, 0x18, 0x2C);
    public Color OverlayScrim { get; init; } = Color.FromArgb(0x38, 0xF0, 0xF4, 0xFA);
}

/// <summary>弹簧参数。</summary>
public readonly struct SpringPreset
{
    public readonly double Duration;   // 秒
    public readonly double Bounce;     // 0 = 无回弹；越大越"跳"
    public SpringPreset(double duration, double bounce) { Duration = duration; Bounce = bounce; }
    public static readonly SpringPreset Snappy = new(0.42, 0.11);
}

/// <summary>全局设计 token（从 EmbeddedResource JSON 读取）。</summary>
public sealed class GlassTokens
{
    public string Version { get; private set; } = "1.0.0";
    public GlassPalette Light { get; private set; } = new();
    public GlassPalette Dark { get; private set; } = new();
    public IReadOnlyDictionary<string, GlassMaterialSpec> Materials { get; private set; } = new Dictionary<string, GlassMaterialSpec>();
    public double ShapeControl { get; private set; } = 14;
    public double ShapeTabBar { get; private set; } = 34;
    public double ShapeCard { get; private set; } = 26;
    public double ShapeSheet { get; private set; } = 34;
    public double ShapeWindow { get; private set; } = 30;
    public double ShapeConcentricInset { get; private set; } = 8;
    public double ShapeIconTile { get; private set; } = 22;
    public SpringPreset SpringSnappy { get; private set; } = new(0.42, 0.11);
    public SpringPreset SpringSmooth { get; private set; } = new(0.62, 0.0);
    public SpringPreset SpringBouncy { get; private set; } = new(0.52, 0.34);
    public SpringPreset SpringPress { get; private set; } = new(0.30, 0.26);
    public SpringPreset SpringMorph { get; private set; } = new(0.46, 0.20);
    public SpringPreset SpringAppear { get; private set; } = new(0.58, 0.22);
    public double PressScale { get; private set; } = 0.955;
    public double Stagger { get; private set; } = 0.045;
    public double ScrollEdgeFade { get; private set; } = 0.9;
    public double FontLargeTitle { get; private set; } = 26;
    public double FontTitle { get; private set; } = 17;
    public double FontHeadline { get; private set; } = 15;
    public double FontBody { get; private set; } = 14;
    public double FontFootnote { get; private set; } = 12;
    public double FontCaption { get; private set; } = 11;

    private static GlassTokens? _current;

    /// <summary>获取（并缓存）从程序集资源加载的 token。</summary>
    public static GlassTokens Current => _current ??= Load();

    public GlassMaterialSpec Material(string name) =>
        Materials.TryGetValue(name, out var spec) ? spec : Materials["regular"];

    private static GlassTokens Load()
    {
        try
        {
            var asm = typeof(GlassTokens).Assembly;
            string? json = null;
            foreach (var res in asm.GetManifestResourceNames())
            {
                if (res.EndsWith("liquid-glass.tokens.json", StringComparison.OrdinalIgnoreCase))
                {
                    using var s = asm.GetManifestResourceStream(res);
                    if (s is not null)
                    {
                        using var reader = new StreamReader(s);
                        json = reader.ReadToEnd();
                    }
                    break;
                }
            }
            if (json is not null) return Parse(json);
        }
        catch
        {
            // 加载失败时使用内置默认值，保证库仍可用
        }
        return new GlassTokens();
    }

    /// <summary>从 JSON 文本解析 token（也便于宿主自定义主题文件）。</summary>
    public static GlassTokens Parse(string json)
    {
        var t = new GlassTokens();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("meta", out var meta) && meta.TryGetProperty("version", out var v))
            t.Version = v.GetString() ?? t.Version;

        if (root.TryGetProperty("palette", out var palette))
        {
            if (palette.TryGetProperty("light", out var l)) t.Light = ReadPalette(l);
            if (palette.TryGetProperty("dark", out var d)) t.Dark = ReadPalette(d);
        }

        if (root.TryGetProperty("material", out var mat))
        {
            var dict = new Dictionary<string, GlassMaterialSpec>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in mat.EnumerateObject())
                dict[prop.Name] = ReadMaterial(prop.Name, prop.Value);
            if (dict.Count > 0) t.Materials = dict;
        }

        if (root.TryGetProperty("shape", out var shape))
        {
            t.ShapeControl = D(shape, "control", t.ShapeControl);
            t.ShapeTabBar = D(shape, "tabBar", t.ShapeTabBar);
            t.ShapeCard = D(shape, "card", t.ShapeCard);
            t.ShapeSheet = D(shape, "sheet", t.ShapeSheet);
            t.ShapeWindow = D(shape, "window", t.ShapeWindow);
            t.ShapeConcentricInset = D(shape, "concentricInset", t.ShapeConcentricInset);
            t.ShapeIconTile = D(shape, "iconTile", t.ShapeIconTile);
        }

        if (root.TryGetProperty("motion", out var motion))
        {
            if (motion.TryGetProperty("springPresets", out var presets))
            {
                t.SpringSnappy = P(presets, "snappy", t.SpringSnappy);
                t.SpringSmooth = P(presets, "smooth", t.SpringSmooth);
                t.SpringBouncy = P(presets, "bouncy", t.SpringBouncy);
                t.SpringPress = P(presets, "press", t.SpringPress);
                t.SpringMorph = P(presets, "morph", t.SpringMorph);
                t.SpringAppear = P(presets, "appear", t.SpringAppear);
            }
            if (motion.TryGetProperty("press", out var press))
                t.PressScale = D(press, "scale", t.PressScale);
            t.Stagger = D(motion, "stagger", t.Stagger);
            t.ScrollEdgeFade = D(motion, "scrollEdgeFade", t.ScrollEdgeFade);
        }

        if (root.TryGetProperty("type", out var type) && type.TryGetProperty("size", out var size))
        {
            t.FontLargeTitle = D(size, "largeTitle", t.FontLargeTitle);
            t.FontTitle = D(size, "title", t.FontTitle);
            t.FontHeadline = D(size, "headline", t.FontHeadline);
            t.FontBody = D(size, "body", t.FontBody);
            t.FontFootnote = D(size, "footnote", t.FontFootnote);
            t.FontCaption = D(size, "caption", t.FontCaption);
        }
        return t;
    }

    private static GlassPalette ReadPalette(JsonElement e) => new()
    {
        Tint = C(e, "tint", Colors.White),
        TintOpacity = D(e, "tintOpacity", 0.34),
        TintOpacityClear = D(e, "tintOpacityClear", 0.14),
        Label = C(e, "label", Color.FromRgb(0x10, 0x13, 0x18)),
        LabelSecondary = C(e, "labelSecondary", Color.FromArgb(0x94, 0x10, 0x13, 0x18)),
        LabelTertiary = C(e, "labelTertiary", Color.FromArgb(0x61, 0x10, 0x13, 0x18)),
        Accent = C(e, "accent", Color.FromRgb(0x0A, 0x7C, 0xFF)),
        AccentLabel = C(e, "accentLabel", Colors.White),
        SystemFill = C(e, "systemFill", Color.FromArgb(0x24, 0x78, 0x82, 0x96)),
        Separator = C(e, "separator", Color.FromArgb(0x1A, 0x10, 0x13, 0x18)),
        Surface = C(e, "surface", Color.FromRgb(0xF3, 0xF4, 0xF7)),
        RimLight = C(e, "rimLight", Color.FromArgb(0xB8, 0xFF, 0xFF, 0xFF)),
        RimShade = C(e, "rimShade", Color.FromArgb(0x1A, 0x0C, 0x12, 0x20)),
        Shadow = C(e, "shadowColor", Color.FromArgb(0x38, 0x0E, 0x18, 0x2C)),
        OverlayScrim = C(e, "overlayScrim", Color.FromArgb(0x38, 0xF0, 0xF4, 0xFA)),
    };

    private static GlassMaterialSpec ReadMaterial(string name, JsonElement e) => new()
    {
        Name = name,
        Blur = D(e, "blur", 24),
        Saturation = D(e, "saturation", 1.72),
        Brightness = D(e, "brightness", 1.04),
        LensScale = D(e, "lensScale", 1.035),
        LensOpacity = D(e, "lensOpacity", 0.45),
        LensBlur = D(e, "lensBlur", 10),
        Dispersion = D(e, "dispersion", 0.16),
        Specular = D(e, "specular", 0.30),
        Noise = D(e, "noise", 0.045),
        RimWidth = D(e, "rimWidth", 1),
        ShadowOpacity = D(e, "shadowOpacity", 1.0),
        ShadowY = D(e, "shadowY", 22),
        ShadowBlur = D(e, "shadowBlur", 46),
        InnerGlow = D(e, "innerGlow", 0.22),
        TintOpacity = D(e, "tintOpacity", 0.50),
    };

    private static double D(JsonElement e, string name, double fallback)
    {
        if (!e.TryGetProperty(name, out var v)) return fallback;
        return v.ValueKind == JsonValueKind.Number ? v.GetDouble() : fallback;
    }

    private static SpringPreset P(JsonElement e, string name, SpringPreset fallback)
    {
        if (!e.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Object) return fallback;
        return new SpringPreset(D(v, "duration", fallback.Duration), D(v, "bounce", fallback.Bounce));
    }

    private static Color C(JsonElement e, string name, Color fallback)
    {
        if (!e.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.String) return fallback;
        return ColorParser.Parse(v.GetString(), fallback);
    }
}

/// <summary>解析 #RRGGBB / #AARRGGBB / rgba(r,g,b,a) 三种写法。</summary>
public static class ColorParser
{
    public static Color Parse(string? text, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        text = text.Trim();

        if (text.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase))
        {
            var inner = text[(text.IndexOf('(') + 1)..].TrimEnd(')');
            var parts = inner.Split(',');
            if (parts.Length >= 3)
            {
                var r = (byte)Math.Clamp((int)double.Parse(parts[0].Trim(), CultureInfo.InvariantCulture), 0, 255);
                var g = (byte)Math.Clamp((int)double.Parse(parts[1].Trim(), CultureInfo.InvariantCulture), 0, 255);
                var b = (byte)Math.Clamp((int)double.Parse(parts[2].Trim(), CultureInfo.InvariantCulture), 0, 255);
                var a = 255;
                if (parts.Length >= 4)
                    a = (byte)Math.Clamp((int)(double.Parse(parts[3].Trim(), CultureInfo.InvariantCulture) * 255), 0, 255);
                return Color.FromArgb(a, r, g, b);
            }
            return fallback;
        }

        if (text.StartsWith("#"))
        {
            var hex = text[1..];
            try
            {
                if (hex.Length == 6)
                    return Color.FromRgb(
                        (byte)Convert.ToInt32(hex[..2], 16),
                        (byte)Convert.ToInt32(hex.Substring(2, 2), 16),
                        (byte)Convert.ToInt32(hex.Substring(4, 2), 16));
                if (hex.Length == 8)
                    return Color.FromArgb(
                        (byte)Convert.ToInt32(hex[..2], 16),
                        (byte)Convert.ToInt32(hex.Substring(2, 2), 16),
                        (byte)Convert.ToInt32(hex.Substring(4, 2), 16),
                        (byte)Convert.ToInt32(hex.Substring(6, 2), 16));
            }
            catch
            {
                return fallback;
            }
        }
        return fallback;
    }

    public static string ToHex(Color c) =>
        c.A == 255
            ? $"#{c.R:X2}{c.G:X2}{c.B:X2}"
            : $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
}
