using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using LiquidGlass.WPF.Animation;
using LiquidGlass.WPF.Theme;
using LiquidGlass.WPF.Primitives;

namespace LiquidGlass.WPF.Controls;

/// <summary>
/// 液态玻璃按钮。按下时整块玻璃做 0.955 的挤压并从按下点扩散一圈光晕，
/// 抬手后由弹簧（duration 0.30 / bounce 0.26）弹回——不使用线性缓动。
/// </summary>
public class GlassButton : Button
{
    public static readonly DependencyProperty MaterialProperty = DependencyProperty.Register(
        nameof(Material), typeof(GlassMaterialKind), typeof(GlassButton),
        new PropertyMetadata(GlassMaterialKind.Regular));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(GlassButton),
        new PropertyMetadata(new CornerRadius(22)));

    public static readonly DependencyProperty IconSymbolProperty = DependencyProperty.Register(
        nameof(IconSymbol), typeof(string), typeof(GlassButton), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PressScaleProperty = DependencyProperty.Register(
        nameof(PressScale), typeof(double), typeof(GlassButton), new PropertyMetadata(1.0));

    public static readonly DependencyProperty GlowOpacityProperty = DependencyProperty.Register(
        nameof(GlowOpacity), typeof(double), typeof(GlassButton), new PropertyMetadata(0.0));

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

    /// <summary>SF Symbols 名称（play / heart / search …），留空则不显示图标。</summary>
    public string IconSymbol
    {
        get => (string)GetValue(IconSymbolProperty);
        set => SetValue(IconSymbolProperty, value);
    }

    public double PressScale
    {
        get => (double)GetValue(PressScaleProperty);
        set => SetValue(PressScaleProperty, value);
    }

    public double GlowOpacity
    {
        get => (double)GetValue(GlowOpacityProperty);
        set => SetValue(GlowOpacityProperty, value);
    }

    public GlassButton()
    {
        DefaultStyleKey = typeof(GlassButton);
        RenderTransformOrigin = new Point(0.5, 0.5);
        RenderTransform = new ScaleTransform(1, 1);
        Cursor = System.Windows.Input.Cursors.Hand;
    }

    private bool _down;

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsPressedProperty)
        {
            var pressed = (bool)e.NewValue;
            if (pressed && !_down) PressDown();
            else if (!pressed && _down) PressUp();
        }
    }

    protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (RenderTransform is ScaleTransform && ActualWidth > 0)
        {
            var p = e.GetPosition(this);
            ((ScaleTransform)RenderTransform).CenterX = p.X;
            ((ScaleTransform)RenderTransform).CenterY = p.Y;
        }
    }

    private void PressDown()
    {
        _down = true;
        var tokens = GlassTokens.Current;
        var target = PressScale <= 0 ? tokens.PressScale : PressScale;
        var from = ((ScaleTransform)RenderTransform).ScaleX;
        SpringAnimator.Animate(from, target, tokens.SpringPress, v =>
        {
            if (RenderTransform is ScaleTransform st) { st.ScaleX = v; st.ScaleY = v; }
            GlowOpacity = Math.Max(0, 1 - Math.Abs(v - target) * 3);
        });
    }

    private void PressUp()
    {
        _down = false;
        var tokens = GlassTokens.Current;
        var from = ((ScaleTransform)RenderTransform).ScaleX;
        SpringAnimator.Animate(from, 1.0, tokens.SpringPress, v =>
        {
            if (RenderTransform is ScaleTransform st) { st.ScaleX = v; st.ScaleY = v; }
            GlowOpacity = Math.Max(0, GlowOpacity - 0.12);
        }, () => GlowOpacity = 0);
    }
}

/// <summary>iOS 风格开关：旋钮带"gel"黏滞（拖动时轻微拉伸），值变化由弹簧驱动。</summary>
public class GlassToggleSwitch : ToggleButton
{
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(GlassToggleSwitch),
        new PropertyMetadata(new CornerRadius(16)));

    public static readonly DependencyProperty KnobProgressProperty = DependencyProperty.Register(
        nameof(KnobProgress), typeof(double), typeof(GlassToggleSwitch),
        new PropertyMetadata(0.0, (d, e) => ((GlassToggleSwitch)d).UpdateKnobOffset()));

    public static readonly DependencyProperty KnobOffsetProperty = DependencyProperty.Register(
        nameof(KnobOffset), typeof(double), typeof(GlassToggleSwitch), new PropertyMetadata(0.0));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>0 = 关，1 = 开；由弹簧动画驱动，模板用它平移旋钮并给轨道染色。</summary>
    public double KnobProgress
    {
        get => (double)GetValue(KnobProgressProperty);
        set => SetValue(KnobProgressProperty, value);
    }

    /// <summary>旋钮的实际像素位移（= 进度 × 可移动距离），模板直接绑定。</summary>
    public double KnobOffset
    {
        get => (double)GetValue(KnobOffsetProperty);
        set => SetValue(KnobOffsetProperty, value);
    }

    public GlassToggleSwitch()
    {
        DefaultStyleKey = typeof(GlassToggleSwitch);
        Cursor = System.Windows.Input.Cursors.Hand;
        SizeChanged += (_, __) => UpdateKnobOffset();
    }

    private void UpdateKnobOffset()
    {
        var travel = Math.Max(0, ActualWidth - 26 - 6);
        KnobOffset = KnobProgress * travel;
    }

    protected override void OnChecked(RoutedEventArgs e)
    {
        base.OnChecked(e);
        Animate(1.0);
    }

    protected override void OnUnchecked(RoutedEventArgs e)
    {
        base.OnUnchecked(e);
        Animate(0.0);
    }

    private void Animate(double target)
    {
        var tokens = GlassTokens.Current;
        SpringAnimator.Animate(KnobProgress, target, tokens.SpringBouncy,
            v => KnobProgress = v);
    }
}

/// <summary>玻璃滑块：轨道为 6px 胶囊，滑块为实心圆，拖动时数值即时更新。</summary>
public class GlassSlider : Slider
{
    public static readonly DependencyProperty MaterialProperty = DependencyProperty.Register(
        nameof(Material), typeof(GlassMaterialKind), typeof(GlassSlider),
        new PropertyMetadata(GlassMaterialKind.Clear));

    public static readonly DependencyProperty TrackHeightProperty = DependencyProperty.Register(
        nameof(TrackHeight), typeof(double), typeof(GlassSlider), new PropertyMetadata(6.0));

    public GlassMaterialKind Material
    {
        get => (GlassMaterialKind)GetValue(MaterialProperty);
        set => SetValue(MaterialProperty, value);
    }

    public double TrackHeight
    {
        get => (double)GetValue(TrackHeightProperty);
        set => SetValue(TrackHeightProperty, value);
    }

    public GlassSlider()
    {
        DefaultStyleKey = typeof(GlassSlider);
        Cursor = System.Windows.Input.Cursors.Hand;
    }
}

/// <summary>小尺寸玻璃标签 / 气泡。</summary>
public class GlassChip : ContentControl
{
    public static readonly DependencyProperty MaterialProperty = DependencyProperty.Register(
        nameof(Material), typeof(GlassMaterialKind), typeof(GlassChip),
        new PropertyMetadata(GlassMaterialKind.Regular));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(GlassChip),
        new PropertyMetadata(new CornerRadius(14)));

    public static readonly DependencyProperty IconSymbolProperty = DependencyProperty.Register(
        nameof(IconSymbol), typeof(string), typeof(GlassChip), new PropertyMetadata(string.Empty));

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

    public string IconSymbol
    {
        get => (string)GetValue(IconSymbolProperty);
        set => SetValue(IconSymbolProperty, value);
    }

    public GlassChip()
    {
        DefaultStyleKey = typeof(GlassChip);
    }
}
