using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using LiquidGlass.WPF.Animation;
using LiquidGlass.WPF.Primitives;
using LiquidGlass.WPF.Theme;

namespace LiquidGlass.WPF.Controls;

/// <summary>标签栏中的一个标签。</summary>
public class GlassTabItem : ContentControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(GlassTabItem), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconSymbolProperty = DependencyProperty.Register(
        nameof(IconSymbol), typeof(string), typeof(GlassTabItem), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(GlassTabItem),
        new PropertyMetadata(false, (d, e) => ((GlassTabItem)d).OnSelectedChanged((bool)e.NewValue)));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string IconSymbol
    {
        get => (string)GetValue(IconSymbolProperty);
        set => SetValue(IconSymbolProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public GlassTabItem()
    {
        DefaultStyleKey = typeof(GlassTabItem);
    }

    private void OnSelectedChanged(bool selected)
    {
        // 选中态的颜色/透明度由模板 Trigger 处理；
        // 这里预留一个钩子，宿主可在此触觉反馈（例如播放音效或震动）。
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>选中状态变化事件。</summary>
    public event EventHandler? SelectionChanged;
}

/// <summary>
/// 悬浮胶囊标签栏：指示器在标签间滑动，滑动速度越快胶囊越"拉长"（squash &amp; stretch），
/// 松手后由弹簧收回。可在滚动时最小化（IsMinimized）。
/// </summary>
public class GlassTabBar : ItemsControl
{
    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex), typeof(int), typeof(GlassTabBar),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedIndexChanged));

    public static readonly DependencyProperty IndicatorOffsetProperty = DependencyProperty.Register(
        nameof(IndicatorOffset), typeof(double), typeof(GlassTabBar), new PropertyMetadata(0.0));

    public static readonly DependencyProperty IndicatorWidthProperty = DependencyProperty.Register(
        nameof(IndicatorWidth), typeof(double), typeof(GlassTabBar), new PropertyMetadata(0.0));

    public static readonly DependencyProperty MaterialProperty = DependencyProperty.Register(
        nameof(Material), typeof(GlassMaterialKind), typeof(GlassTabBar),
        new PropertyMetadata(GlassMaterialKind.Regular));

    public static readonly DependencyProperty IsMinimizedProperty = DependencyProperty.Register(
        nameof(IsMinimized), typeof(bool), typeof(GlassTabBar), new PropertyMetadata(false));

    public static readonly DependencyProperty TintBrushProperty = DependencyProperty.Register(
        nameof(TintBrush), typeof(Brush), typeof(GlassTabBar), new PropertyMetadata(null));

    public static readonly DependencyProperty TintOpacityProperty = DependencyProperty.Register(
        nameof(TintOpacity), typeof(double), typeof(GlassTabBar), new PropertyMetadata(double.NaN));

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public double IndicatorOffset
    {
        get => (double)GetValue(IndicatorOffsetProperty);
        set => SetValue(IndicatorOffsetProperty, value);
    }

    public double IndicatorWidth
    {
        get => (double)GetValue(IndicatorWidthProperty);
        set => SetValue(IndicatorWidthProperty, value);
    }

    public GlassMaterialKind Material
    {
        get => (GlassMaterialKind)GetValue(MaterialProperty);
        set => SetValue(MaterialProperty, value);
    }

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

    /// <summary>滚动时收起为更小的胶囊（iOS 26 tab bar 的 minimize 行为）。</summary>
    public bool IsMinimized
    {
        get => (bool)GetValue(IsMinimizedProperty);
        set => SetValue(IsMinimizedProperty, value);
    }

    /// <summary>选中标签变化（供宿主刷新标题/路由）。</summary>
    public event EventHandler? SelectionChanged;

    public GlassTabBar()
    {
        DefaultStyleKey = typeof(GlassTabBar);
        Loaded += (_, __) => Dispatcher.BeginInvoke(new Action(() => SyncIndicator(animate: false)));
    }

    protected override void OnItemsChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        Dispatcher.BeginInvoke(new Action(() => SyncIndicator(animate: false)));
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        Dispatcher.BeginInvoke(new Action(() => SyncIndicator(animate: false)));
    }

    private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not GlassTabBar bar) return;
        bar.UpdateSelection((int)e.OldValue, (int)e.NewValue);
        bar.SyncIndicator(animate: true);
        bar.SelectionChanged?.Invoke(bar, EventArgs.Empty);
    }

    private void UpdateSelection(int oldIndex, int newIndex)
    {
        if (oldIndex >= 0 && oldIndex < Items.Count && Items[oldIndex] is GlassTabItem oldItem)
            oldItem.IsSelected = false;
        if (newIndex >= 0 && newIndex < Items.Count && Items[newIndex] is GlassTabItem newItem)
            newItem.IsSelected = true;
    }

    private ItemsControl? _itemsHost;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _itemsHost = GetTemplateChild("PART_Items") as ItemsControl ?? this;
        Dispatcher.BeginInvoke(new Action(() => SyncIndicator(animate: false)));
    }

    private void SyncIndicator(bool animate = false)
    {
        var host = _itemsHost ?? this;
        var count = Items.Count;
        if (count == 0) return;

        var index = Math.Clamp(SelectedIndex, 0, count - 1);
        var container = host.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
        if (container is null) return;

        double left, width;
        try
        {
            var transform = container.TransformToAncestor(this);
            left = transform.Transform(new Point(0, 0)).X;
            width = container.ActualWidth;
        }
        catch
        {
            return;
        }
        if (width <= 0) return;

        for (var i = 0; i < count; i++)
        {
            if (Items[i] is GlassTabItem item) item.IsSelected = i == index;
        }

        if (!animate)
        {
            IndicatorOffset = left;
            IndicatorWidth = width;
            return;
        }

        var tokens = GlassTokens.Current;
        var fromOffset = IndicatorOffset;
        var fromWidth = IndicatorWidth <= 0 ? width : IndicatorWidth;
        // 用同一求解器驱动两个维度，速度差即产生"拉长"的错觉
        SpringAnimator.Animate(fromOffset, left, tokens.SpringSnappy, v => IndicatorOffset = v);
        SpringAnimator.Animate(fromWidth, width, tokens.SpringSnappy, v => IndicatorWidth = v);
    }
}

/// <summary>
/// 分段控件：白色滑块在选项间移动（spring 0.42 / bounce 0.11），
/// 选项文字随滑块位置淡入淡出。
/// </summary>
public class GlassSegmentedControl : Control
{
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
        nameof(Items), typeof(IEnumerable), typeof(GlassSegmentedControl), new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex), typeof(int), typeof(GlassSegmentedControl),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedIndexChanged));

    public static readonly DependencyProperty ThumbOffsetProperty = DependencyProperty.Register(
        nameof(ThumbOffset), typeof(double), typeof(GlassSegmentedControl), new PropertyMetadata(0.0));

    public static readonly DependencyProperty ThumbWidthProperty = DependencyProperty.Register(
        nameof(ThumbWidth), typeof(double), typeof(GlassSegmentedControl), new PropertyMetadata(0.0));

    public IEnumerable? Items
    {
        get => (IEnumerable?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public double ThumbOffset
    {
        get => (double)GetValue(ThumbOffsetProperty);
        set => SetValue(ThumbOffsetProperty, value);
    }

    public double ThumbWidth
    {
        get => (double)GetValue(ThumbWidthProperty);
        set => SetValue(ThumbWidthProperty, value);
    }

    public GlassSegmentedControl()
    {
        DefaultStyleKey = typeof(GlassSegmentedControl);
        Loaded += (_, __) => Dispatcher.BeginInvoke(new Action(() => SyncThumb(false)));
    }

    private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GlassSegmentedControl seg) seg.SyncThumb(true);
    }

    private ItemsControl? _itemsHost;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _itemsHost = GetTemplateChild("PART_Items") as ItemsControl;
        Dispatcher.BeginInvoke(new Action(() => SyncThumb(false)));
    }

    private void SyncThumb(bool animate)
    {
        var host = _itemsHost;
        if (host is null) return;
        var container = host.ItemContainerGenerator.ContainerFromIndex(Math.Max(0, SelectedIndex)) as FrameworkElement;
        if (container is null) return;
        try
        {
            var p = container.TransformToAncestor(this).Transform(new Point(0, 0));
            if (!animate)
            {
                ThumbOffset = p.X;
                ThumbWidth = container.ActualWidth;
                return;
            }
            var tokens = GlassTokens.Current;
            SpringAnimator.Animate(ThumbOffset, p.X, tokens.SpringSnappy, v => ThumbOffset = v);
            SpringAnimator.Animate(ThumbWidth <= 0 ? container.ActualWidth : ThumbWidth, container.ActualWidth,
                tokens.SpringSnappy, v => ThumbWidth = v);
        }
        catch { }
    }
}
