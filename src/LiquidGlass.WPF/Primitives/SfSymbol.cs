using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace LiquidGlass.WPF.Primitives;

/// <summary>
/// 一组手绘的 SF Symbols 风格矢量图标（24×24 画布，1.6px 描边）。
/// 用名称即可：<c>&lt;lg:SfSymbol Symbol="play" /&gt;</c>，颜色自动跟随 Foreground。
/// </summary>
public class SfSymbol : FrameworkElement
{
    private static readonly Dictionary<string, string> Glyphs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["play"] = "M8.5 5.2 L18.8 12 L8.5 18.8 Z",
        ["pause"] = "M9 5.5 V18.5 M15 5.5 V18.5",
        ["heart"] = "M12 19.6 C7 15.6 4 13 4 9.7 A4.6 4.6 0 0 1 12 6.9 A4.6 4.6 0 0 1 20 9.7 C20 13 17 15.6 12 19.6 Z",
        ["search"] = "M10.6 4.4 A6.2 6.2 0 1 1 10.6 16.8 A6.2 6.2 0 1 1 10.6 4.4 Z M15.2 15.2 L19.6 19.6",
        ["ellipsis"] = "M6.2 12 A1.25 1.25 0 1 0 8.7 12 A1.25 1.25 0 1 0 6.2 12 Z M10.75 12 A1.25 1.25 0 1 0 13.25 12 A1.25 1.25 0 1 0 10.75 12 Z M15.3 12 A1.25 1.25 0 1 0 17.8 12 A1.25 1.25 0 1 0 15.3 12 Z",
        ["chevron-down"] = "M7 9.5 L12 14.5 L17 9.5",
        ["chevron-up"] = "M7 14.5 L12 9.5 L17 14.5",
        ["chevron-left"] = "M14.5 6.5 L9.5 12 L14.5 17.5",
        ["chevron-right"] = "M9.5 6.5 L14.5 12 L9.5 17.5",
        ["check"] = "M5 12.4 L9.8 17.2 L19 7.6",
        ["plus"] = "M12 5.2 V18.8 M5.2 12 H18.8",
        ["share"] = "M12 4.4 V14.6 M8 8.4 L12 4.4 L16 8.4 M5.4 13.6 V18.6 H18.6 V13.6",
        ["sliders"] = "M5 7.5 H19 M5 12 H19 M5 16.5 H19 M9 5.4 V9.6 M15 10 V14.2 M8 14.4 V18.6",
        ["memories"] = "M4.6 6.4 H19.4 V17.6 H4.6 Z M9 12.6 A2.2 2.2 0 1 1 9 12.61 Z M12.6 11.2 L18.2 15.6",
        ["playlist"] = "M5 7.4 H15.6 M5 12 H15.6 M5 16.6 H11.6 M17.4 12.6 L19.6 14.8 L17.4 17",
        ["photos"] = "M4.6 6.4 H19.4 V17.6 H4.6 Z M4.6 14.4 L9.6 10.2 L13 13 L16 10.6 L19.4 13.4",
        ["search-tab"] = "M10.4 4.6 A6 6 0 1 1 10.4 16.6 A6 6 0 1 1 10.4 4.6 Z M15 15.2 L19.4 19.6",
        ["sparkles"] = "M12 4.2 L13.4 8.6 L17.8 10 L13.4 11.4 L12 15.8 L10.6 11.4 L6.2 10 L10.6 8.6 Z M17.6 15.2 L18.3 17.2 L20.3 17.9 L18.3 18.6 L17.6 20.6 L16.9 18.6 L14.9 17.9 L16.9 17.2 Z",
        ["moon"] = "M15.4 4.2 A8 8 0 1 0 19.8 15.2 A7 7 0 1 1 15.4 4.2 Z",
        ["bolt"] = "M13 3.6 L6.4 13.4 H11 L10.4 20.4 L17.6 10.2 H12.6 Z",
        ["wifi"] = "M5 10.4 A9.4 9.4 0 0 1 19 10.4 M7.8 13.4 A6.2 6.2 0 0 1 16.2 13.4 M10.6 16.4 A3 3 0 0 1 13.4 16.4 M12 19.2 A.9 .9 0 1 0 12.01 19.2 Z",
        ["sun"] = "M12 8 A4 4 0 1 0 12 16 A4 4 0 1 0 12 8 Z M12 3.6 V5.4 M12 18.6 V20.4 M3.6 12 H5.4 M18.6 12 H20.4 M6.1 6.1 L7.4 7.4 M16.6 16.6 L17.9 17.9 M17.9 6.1 L16.6 7.4 M7.4 16.6 L6.1 17.9",
        ["airplay"] = "M5.6 15.4 V8.6 H18.4 V15.4 M8 15.4 L12 19.4 L16 15.4 Z",
        ["trash"] = "M6.4 7.4 H17.6 M9.4 7.4 V5.6 H14.6 V7.4 M7.6 7.4 L8.4 19 H15.6 L16.4 7.4",
        ["gear"] = "M12 8.6 A3.4 3.4 0 1 0 12 15.4 A3.4 3.4 0 1 0 12 8.6 Z M12 4.4 V6.6 M12 17.4 V19.6 M4.4 12 H6.6 M17.4 12 H19.6 M6.6 6.6 L8.1 8.1 M15.9 15.9 L17.4 17.4 M17.4 6.6 L15.9 8.1 M8.1 15.9 L6.6 17.4",
        ["download"] = "M12 4.4 V14.6 M7.8 10.6 L12 14.8 L16.2 10.6 M5.6 18.4 H18.4",
        ["close"] = "M7 7 L17 17 M17 7 L7 17",
    };

    public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(
        nameof(Symbol), typeof(string), typeof(SfSymbol),
        new FrameworkPropertyMetadata("play", FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(SfSymbol),
        new FrameworkPropertyMetadata(1.6, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(bool), typeof(SfSymbol),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Symbol
    {
        get => (string)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>实心渲染（如 play / heart 选中态）。</summary>
    public bool Fill
    {
        get => (bool)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    private Geometry? _geometry;

    protected override void OnRender(DrawingContext dc)
    {
        var renderSize = this.RenderSize;
        if (renderSize.Width <= 0 || renderSize.Height <= 0) return;
        _geometry ??= Geometry.Parse(Glyphs.TryGetValue(Symbol, out var d) ? d : Glyphs["play"]);

        var brush = TextElement.GetForeground(this);
        if (brush is null) return;

        var scale = Math.Min(renderSize.Width, renderSize.Height) / 24.0;
        dc.PushTransform(new TranslateTransform(
            (renderSize.Width - 24 * scale) / 2, (renderSize.Height - 24 * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        var pen = new Pen(brush, StrokeThickness);
        pen.StartLineCap = PenLineCap.Round;
        pen.EndLineCap = PenLineCap.Round;
        pen.LineJoin = PenLineJoin.Round;
        dc.DrawGeometry(Fill ? brush : null, Fill ? null : pen, _geometry);
        dc.Pop();
        dc.Pop();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == SymbolProperty || e.Property == FillProperty) _geometry = null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var side = double.IsInfinity(availableSize.Width) || double.IsNaN(availableSize.Width)
            ? 20
            : Math.Min(availableSize.Width, double.IsInfinity(availableSize.Height) ? 20 : availableSize.Height);
        return new Size(side, side);
    }
}
