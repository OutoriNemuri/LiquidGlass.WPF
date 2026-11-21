using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using LiquidGlass.WPF.Animation;
using LiquidGlass.WPF.Effects;
using LiquidGlass.WPF.Primitives;
using LiquidGlass.WPF.Theme;

namespace LiquidGlass.WPF.Controls;

/// <summary>玻璃卡片容器：内容区自动获得 concentric 圆角（子元素可继承 InnerCornerRadius）。</summary>
public class GlassCard : ContentControl
{
    public static readonly DependencyProperty MaterialProperty = DependencyProperty.Register(
        nameof(Material), typeof(GlassMaterialKind), typeof(GlassCard),
        new PropertyMetadata(GlassMaterialKind.Regular));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(GlassCard), new PropertyMetadata(new CornerRadius(26)));

    /// <summary>子元素的 concentric 半径 = 父半径 − Inset（默认取 token 的 concentricInset）。</summary>
    public static readonly DependencyProperty InnerCornerRadiusProperty = DependencyProperty.Register(
        nameof(InnerCornerRadius), typeof(CornerRadius), typeof(GlassCard), new PropertyMetadata(new CornerRadius(18)));

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

    public CornerRadius InnerCornerRadius
    {
        get => (CornerRadius)GetValue(InnerCornerRadiusProperty);
        set => SetValue(InnerCornerRadiusProperty, value);
    }

    public GlassCard()
    {
        DefaultStyleKey = typeof(GlassCard);
        SizeChanged += (_, __) => UpdateInnerRadius();
    }

    private void UpdateInnerRadius()
    {
        var inset = GlassTokens.Current.ShapeConcentricInset;
        var outer = CornerRadius.TopLeft;
        InnerCornerRadius = new CornerRadius(Math.Max(0, outer - inset));
    }
}

/// <summary>
/// 玻璃窗口：把系统级材质（Win11 Mica / Acrylic，Win10 亚克力）接到 WPF 窗口上。
/// 注意：开启系统材质后必须把 Window.Background 设为透明，材质才透得出来。
/// </summary>
public class GlassWindow : Window
{
    public static readonly DependencyProperty SystemBackdropProperty = DependencyProperty.Register(
        nameof(SystemBackdrop), typeof(DwmInterop.BackdropType), typeof(GlassWindow),
        new PropertyMetadata(DwmInterop.BackdropType.Acrylic));

    public static readonly DependencyProperty UseSystemBackdropProperty = DependencyProperty.Register(
        nameof(UseSystemBackdrop), typeof(bool), typeof(GlassWindow), new PropertyMetadata(true));

    public DwmInterop.BackdropType SystemBackdrop
    {
        get => (DwmInterop.BackdropType)GetValue(SystemBackdropProperty);
        set => SetValue(SystemBackdropProperty, value);
    }

    public bool UseSystemBackdrop
    {
        get => (bool)GetValue(UseSystemBackdropProperty);
        set => SetValue(UseSystemBackdropProperty, value);
    }

    public GlassWindow()
    {
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brushes.Transparent;
        SourceInitialized += (_, __) => { ClampToWorkArea(); ApplySystemMaterial(); };
        Activated += (_, __) => ApplySystemMaterial();
    }

    /// <summary>把窗口收进屏幕工作区：否则 1296×804 这类尺寸在 1366×768 上会被裁掉底边。</summary>
    private void ClampToWorkArea()
    {
        var area = SystemParameters.WorkArea;
        var maxWidth = Math.Max(320, area.Width - 16);
        var maxHeight = Math.Max(240, area.Height - 16);

        if (Width <= 0 || double.IsNaN(Width)) Width = maxWidth;
        if (Height <= 0 || double.IsNaN(Height)) Height = maxHeight;
        if (Width > maxWidth) Width = maxWidth;
        if (Height > maxHeight) Height = maxHeight;

        if (Left + Width > area.Right) Left = area.Right - Width;
        if (Top + Height > area.Bottom) Top = area.Bottom - Height;
        if (Left < area.Left) Left = area.Left;
        if (Top < area.Top) Top = area.Top;
    }

    private void ApplySystemMaterial()
    {
        if (!UseSystemBackdrop) return;
        var tint = GlassTheme.Palette.Tint;
        // Win10 的亚克力是全窗口覆盖层：alpha 太高会把整个界面洗成奶白色，取 25% 只做轻微环境色。
        var accent = Color.FromArgb(64, tint.R, tint.G, tint.B);
        DwmInterop.EnableSystemBackdrop(this, SystemBackdrop, accent);
        DwmInterop.SetRoundedCorners(this, true);
        DwmInterop.ExtendFrameIntoClientArea(this);
    }
}

/// <summary>
/// 滚动边缘效果（iOS 26 新增）：内容与玻璃交界处不做遮挡、不压暗，
/// 只做一条提示性渐变，告诉用户"下面还有内容"。
/// </summary>
public class GlassScrollEdgeEffect : Control
{
    public enum EdgeSide { Top, Bottom }

    public static readonly DependencyProperty SideProperty = DependencyProperty.Register(
        nameof(Side), typeof(EdgeSide), typeof(GlassScrollEdgeEffect),
        new PropertyMetadata(EdgeSide.Top, (d, e) => ((GlassScrollEdgeEffect)d).InvalidateVisual()));

    public static new readonly DependencyProperty HeightProperty = DependencyProperty.Register(
        nameof(Height), typeof(double), typeof(GlassScrollEdgeEffect), new PropertyMetadata(64.0));

    public static readonly DependencyProperty StrengthProperty = DependencyProperty.Register(
        nameof(Strength), typeof(double), typeof(GlassScrollEdgeEffect), new PropertyMetadata(0.55));

    public EdgeSide Side
    {
        get => (EdgeSide)GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    public new double Height
    {
        get => (double)GetValue(HeightProperty);
        set => SetValue(HeightProperty, value);
    }

    public double Strength
    {
        get => (double)GetValue(StrengthProperty);
        set => SetValue(StrengthProperty, value);
    }

    public GlassScrollEdgeEffect()
    {
        DefaultStyleKey = typeof(GlassScrollEdgeEffect);
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = Height;
        if (w <= 0 || h <= 0) return;
        var scrim = GlassTheme.Palette.OverlayScrim;
        var a0 = (byte)Math.Clamp((int)(Strength * 255), 0, 255);
        var start = Color.FromArgb(a0, scrim.R, scrim.G, scrim.B);
        var end = Color.FromArgb(0, scrim.R, scrim.G, scrim.B);
        var p0 = Side == EdgeSide.Top ? new Point(0, 0) : new Point(0, 1);
        var p1 = Side == EdgeSide.Top ? new Point(0, 1) : new Point(0, 0);
        dc.DrawRectangle(new LinearGradientBrush(start, end, p0, p1), null, new Rect(0, 0, w, h));
    }
}

/// <summary>玻璃下拉框：Popup 以"材质化出现"方式展开（缩放 + 透明度 + 弹簧）。</summary>
public class GlassComboBox : ComboBox
{
    public static readonly DependencyProperty MaterialProperty = DependencyProperty.Register(
        nameof(Material), typeof(GlassMaterialKind), typeof(GlassComboBox),
        new PropertyMetadata(GlassMaterialKind.Clear));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(GlassComboBox), new PropertyMetadata(new CornerRadius(14)));

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

    public GlassComboBox()
    {
        DefaultStyleKey = typeof(GlassComboBox);
    }

    private Popup? _popup;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _popup = GetTemplateChild("PART_Popup") as Popup;
        if (_popup is not null)
        {
            _popup.Opened += (_, __) => AnimatePopup(incoming: true);
            _popup.Closed += (_, __) => AnimatePopup(incoming: false);
        }
    }

    private void AnimatePopup(bool incoming)
    {
        var child = _popup?.Child as FrameworkElement;
        if (child is null) return;
        child.RenderTransformOrigin = new Point(0.5, 0.0);
        var scale = new ScaleTransform(incoming ? 0.96 : 1.0, incoming ? 0.96 : 1.0);
        child.RenderTransform = scale;
        child.Opacity = incoming ? 0 : 1;
        var tokens = GlassTokens.Current;
        var preset = tokens.SpringMorph;

        SpringAnimator.Animate(scale.ScaleX, 1.0, preset, v => { scale.ScaleX = v; scale.ScaleY = v; });
        var fade = new System.Windows.Media.Animation.DoubleAnimation(incoming ? 1.0 : 0.0,
            TimeSpan.FromSeconds(preset.Duration))
        { EasingFunction = new SpringEase { DurationSeconds = preset.Duration, Bounce = preset.Bounce } };
        child.BeginAnimation(UIElement.OpacityProperty, fade);
    }
}
