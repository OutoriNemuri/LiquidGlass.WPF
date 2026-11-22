using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using LiquidGlass.WPF.Effects;
using LiquidGlass.WPF.Theme;

namespace LiquidGlass.WPF.Primitives;

public enum GlassMaterialKind { Regular, Clear, Prominent, Hard }
public enum GlassAppearance { Auto, Light, Dark }

/// <summary>
/// 玻璃基元：一个带 6 个渲染通道的容器（背景捕获 → 模糊 → 透镜 → 色调 → 镜面 → 描边）。
/// 所有几何形状/材质参数都来自 token，控件层只做组合，不写死任何视觉常量。
/// </summary>
public class GlassSurface : ContentControl
{
    public static readonly DependencyProperty MaterialProperty = DependencyProperty.Register(
        nameof(Material), typeof(GlassMaterialKind), typeof(GlassSurface),
        new PropertyMetadata(GlassMaterialKind.Regular, OnVisualChanged));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(GlassSurface),
        new PropertyMetadata(new CornerRadius(14), OnVisualChanged));

    public static readonly DependencyProperty IsLiveProperty = DependencyProperty.Register(
        nameof(IsLive), typeof(bool), typeof(GlassSurface), new PropertyMetadata(false, OnLiveChanged));

    public static readonly DependencyProperty LiveFrameRateProperty = DependencyProperty.Register(
        nameof(LiveFrameRate), typeof(int), typeof(GlassSurface), new PropertyMetadata(30, OnLiveChanged));

    public static readonly DependencyProperty AppearanceProperty = DependencyProperty.Register(
        nameof(Appearance), typeof(GlassAppearance), typeof(GlassSurface),
        new PropertyMetadata(GlassAppearance.Auto, OnVisualChanged));

    public static readonly DependencyProperty EffectiveAppearanceProperty = DependencyProperty.Register(
        nameof(EffectiveAppearance), typeof(GlassAppearance), typeof(GlassSurface),
        new PropertyMetadata(GlassAppearance.Light));

    /// <summary>自定义色调画刷（例如深色胶囊标签栏）；为空时使用调色板 tint。</summary>
    public static readonly DependencyProperty TintBrushProperty = DependencyProperty.Register(
        nameof(TintBrush), typeof(Brush), typeof(GlassSurface),
        new PropertyMetadata(null, OnVisualChanged));

    /// <summary>自定义色调不透明度（0..1）；NaN 表示使用材质 token。</summary>
    public static readonly DependencyProperty TintOpacityProperty = DependencyProperty.Register(
        nameof(TintOpacity), typeof(double), typeof(GlassSurface),
        new PropertyMetadata(double.NaN, OnVisualChanged));

    public GlassMaterialKind Material
    {
        get => (GlassMaterialKind)GetValue(MaterialProperty);
        set => SetValue(MaterialProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>实时捕获背景（滚动、视频、动画壁纸时需要开启）。</summary>
    public bool IsLive
    {
        get => (bool)GetValue(IsLiveProperty);
        set => SetValue(IsLiveProperty, value);
    }

    public int LiveFrameRate
    {
        get => (int)GetValue(LiveFrameRateProperty);
        set => SetValue(LiveFrameRateProperty, value);
    }

    public GlassAppearance Appearance
    {
        get => (GlassAppearance)GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    /// <summary>只读：实际生效的明暗外观（Auto 时根据背景亮度推断）。</summary>
    public GlassAppearance EffectiveAppearance => (GlassAppearance)GetValue(EffectiveAppearanceProperty);

    public Brush? TintBrush
    {
        get => (Brush?)GetValue(TintBrushProperty);
        set => SetValue(TintBrushProperty, value);
    }

    public double TintOpacity
    {
        get => (double)GetValue(TintOpacityProperty);
        set => SetValue(TintOpacityProperty, value);
    }

    public GlassSurface()
    {
        DefaultStyleKey = typeof(GlassSurface);
        GlassTheme.Register(this);
        SizeChanged += (_, __) => ApplyGeometry();
        Loaded += (_, __) => { Refresh(); RestartTimer(); };
        Unloaded += (_, __) => StopTimer();
    }

    private Border? _backdrop;
    private Border? _lens;
    private Border? _tint;
    private Border? _spec;
    private Border? _dispersion;
    private Border? _rim;
    private Grid? _clipHost;
    private DispatcherTimer? _timer;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _backdrop = GetTemplateChild("PART_Backdrop") as Border;
        _lens = GetTemplateChild("PART_Lens") as Border;
        _tint = GetTemplateChild("PART_Tint") as Border;
        _spec = GetTemplateChild("PART_Spec") as Border;
        _dispersion = GetTemplateChild("PART_Dispersion") as Border;
        _rim = GetTemplateChild("PART_Rim") as Border;
        _clipHost = GetTemplateChild("PART_ClipHost") as Grid;
        ApplyGeometry();
        RestartTimer();
        Refresh();
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GlassSurface s)
        {
            s.ApplyGeometry();
            s.Refresh();
        }
    }

    private static void OnLiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GlassSurface s) s.RestartTimer();
    }

    private void RestartTimer()
    {
        StopTimer();
        if (!IsLive || !IsLoaded) return;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / Math.Max(4, LiveFrameRate)) };
        _timer.Tick += (_, __) => Refresh();
        _timer.Start();
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
    }

    /// <summary>把圆角与描边几何写进模板元素。</summary>
    private void ApplyGeometry()
    {
        if (_clipHost is null) return;
        var r = CornerRadius;
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var rect = new RectangleGeometry(new Rect(0, 0, w, h))
        {
            RadiusX = Math.Max(0, r.TopLeft),
            RadiusY = Math.Max(0, r.TopLeft),
        };
        _clipHost.Clip = rect;

        if (_rim is not null)
        {
            var spec = Spec;
            _rim.CornerRadius = r;
            _rim.BorderThickness = new Thickness(spec.RimWidth);
        }
    }

    private GlassMaterialSpec Spec => GlassTokens.Current.Material(Material.ToString().ToLowerInvariant());

    /// <summary>重新合成所有图层：捕获背景 → 判断明暗 → 上色。</summary>
    public void Refresh()
    {
        if (!IsLoaded) return;
        var spec = Spec;
        var palette = GlassTheme.Palette;

        // 1) 背景捕获（缓存 + 可选实时刷新）
        if (_backdrop is not null)
        {
            var bitmap = BackdropCapture.Capture(this, scale: 0.4);
            if (bitmap is not null)
            {
                _backdrop.Background = new ImageBrush(bitmap) { Stretch = Stretch.Fill };
                _backdrop.Effect = GlassTheme.ReduceTransparency || spec.Blur <= 0
                    ? null
                    : new BlurEffect { Radius = spec.Blur * 0.4, RenderingBias = RenderingBias.Performance };
                _backdrop.Opacity = GlassTheme.ReduceTransparency ? 0 : 1;

                // 2) 亮度自适应：regular glass 会感知背景明度并翻转内容颜色
                if (Appearance == GlassAppearance.Auto)
                {
                    var lum = BackdropCapture.AverageLuminance(bitmap);
                    var effective = lum < 0.42 ? GlassAppearance.Dark : GlassAppearance.Light;
                    if (EffectiveAppearance != effective)
                    {
                        SetValue(EffectiveAppearanceProperty, effective);
                        // 背景偏暗 → 文字提亮；背景偏亮 → 用主题强调文字色
                        Foreground = new SolidColorBrush(effective == GlassAppearance.Dark
                            ? Color.FromRgb(0xF2, 0xF4, 0xF8)
                            : palette.Label);
                    }
                }
                else if (EffectiveAppearance != Appearance)
                {
                    SetValue(EffectiveAppearanceProperty, Appearance);
                }
            }
            else
            {
                _backdrop.Background = null;
                _backdrop.Effect = null;
            }
        }

        // 3) 透镜层：同一张背景的放大副本，但只在边缘环带可见。
        //    Apple 的 Liquid Glass 是"边缘弯曲"（bending of edges），
        //    整面放大只会变成一层灰雾，所以用蒙版限制成环带。
        if (_lens is not null)
        {
            _lens.Background = _backdrop?.Background;
            var scale = spec.LensScale <= 0 ? 1 : spec.LensScale;
            if (_lens.RenderTransform is not ScaleTransform st)
            {
                st = new ScaleTransform(1, 1);
                _lens.RenderTransformOrigin = new Point(0.5, 0.5);
                _lens.RenderTransform = st;
            }
            st.ScaleX = scale;
            st.ScaleY = scale;
            _lens.Opacity = GlassTheme.ReduceTransparency ? 0 : Math.Min(0.85, 0.25 + spec.LensOpacity * 0.75);
            if (spec.LensBlur > 0)
                _lens.Effect = new BlurEffect { Radius = spec.LensBlur * 0.35 };
            else
                _lens.Effect = null;
            _lens.OpacityMask = BuildRingMask(ActualWidth, ActualHeight, CornerRadius.TopLeft);
        }

        // 4) 色调层（可读性保障）
        if (_tint is not null)
        {
            if (TintBrush is Brush custom)
            {
                _tint.Background = custom;
                _tint.Opacity = double.IsNaN(TintOpacity) ? 1.0 : Math.Clamp(TintOpacity, 0, 1);
            }
            else
            {
                var baseOpacity = Material == GlassMaterialKind.Clear ? palette.TintOpacityClear : spec.TintOpacity;
                var opacity = GlassTheme.ReduceTransparency ? 0.96 : baseOpacity;
                // 自适应：深色背景上玻璃要更"实"一点，否则白字读不清（HIG legibility）
                var tintColor = Material == GlassMaterialKind.Prominent ? palette.Accent : palette.Tint;
                if (Material != GlassMaterialKind.Prominent && EffectiveAppearance == GlassAppearance.Dark)
                {
                    tintColor = Color.FromRgb(0x0B, 0x0E, 0x14);
                    opacity = GlassTheme.ReduceTransparency ? 0.96 : Math.Min(0.72, baseOpacity + 0.16);
                }
                _tint.Background = new SolidColorBrush(WithAlpha(tintColor, (byte)Math.Round(opacity * 255)));
                _tint.Opacity = 1;
            }
        }

        // 5) 镜面高光
        if (_spec is not null)
        {
            var alpha = (byte)Math.Round(Math.Clamp(spec.Specular, 0, 1) * (GlassTheme.IncreaseContrast ? 0.48 : 0.62) * 255);
            _spec.Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(alpha, 255, 255, 255), 0),
                    new GradientStop(Color.FromArgb((byte)(alpha * 0.20), 255, 255, 255), 0.38),
                    new GradientStop(Colors.Transparent, 0.68),
                    // 底部回光：玻璃边缘让背景折进来的一点亮度
                    new GradientStop(Color.FromArgb((byte)(alpha * 0.30), 255, 255, 255), 1),
                },
                new Point(0, 0), new Point(0, 1));
            _spec.Opacity = 1;
        }

        // 5b) 色散：边缘一圈暖→冷的偏移，模拟折射率随波长变化
        if (_dispersion is not null)
        {
            _dispersion.CornerRadius = CornerRadius;
            _dispersion.Opacity = GlassTheme.ReduceTransparency ? 0 : 0.70;
            _dispersion.BorderBrush = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(72, 255, 176, 122), 0.0),
                    new GradientStop(Color.FromArgb(30, 255, 255, 255), 0.45),
                    new GradientStop(Color.FromArgb(64, 128, 200, 255), 1.0),
                },
                new Point(0, 0), new Point(1, 1));
        }

        // 6) 描边（上亮下暗，制造厚度）
        if (_rim is not null)
        {
            var light = EffectiveAppearance == GlassAppearance.Dark
                ? WithAlpha(palette.RimLight, (byte)Math.Min(255, palette.RimLight.A + 40))
                : palette.RimLight;
            _rim.BorderBrush = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(light, 0),
                    new GradientStop(WithAlpha(palette.RimShade, palette.RimShade.A), 0.55),
                    new GradientStop(WithAlpha(palette.RimShade, palette.RimShade.A), 1),
                },
                new Point(0, 0), new Point(0, 1));

            // 投影不再挂在内容宿主上：Effect 会连文字一起模糊成"重影"，
            // 而且每个玻璃各一份 DropShadowEffect 是主要性能开销。改为纯描边。
            if (_clipHost is not null)
                _clipHost.Effect = null;
        }
    }

    /// <summary>圆角矩形的"环带"蒙版：边缘一圈不透明，中间透明。</summary>
    private static Brush BuildRingMask(double w, double h, double radius)
    {
        if (w <= 0 || h <= 0) return Brushes.White;
        radius = Math.Max(0, radius);
        var band = Math.Clamp(Math.Min(w, h) * 0.13, 5.0, 26.0);
        var outer = new RectangleGeometry(new Rect(0, 0, w, h), radius, radius);
        var innerRect = new Rect(band, band, Math.Max(1, w - band * 2), Math.Max(1, h - band * 2));
        var inner = new RectangleGeometry(innerRect,
            Math.Max(2, radius - band), Math.Max(2, radius - band));
        var ring = new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
        var drawing = new GeometryDrawing(Brushes.White, null, ring);
        return new DrawingBrush(drawing)
        {
            Stretch = Stretch.None,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top,
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, w, h),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, w, h),
        };
    }

    private static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);
}
