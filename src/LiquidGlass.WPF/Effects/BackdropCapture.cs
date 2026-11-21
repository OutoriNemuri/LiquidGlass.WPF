using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LiquidGlass.WPF.Primitives;

namespace LiquidGlass.WPF.Effects;

/// <summary>
/// 把「玻璃背后真正发生了什么」抓成位图。WPF 没有系统级的 backdrop-filter，
/// 因此这里用 RenderTargetBitmap 对内容层做降采样捕获，再交给 GlassSurface 做模糊/透镜。
/// 为获得稳定帧率，捕获结果在 16ms 内会被复用（同一帧多次刷新只渲染一次）。
/// </summary>
public static class BackdropCapture
{
    private sealed class Entry
    {
        public BitmapSource? Bitmap;
        public long Stamp;
        public int PixelWidth;
        public int PixelHeight;
    }

    private static readonly Dictionary<object, Entry> Cache = new();

    #region 附加属性：标记可被捕获的内容层

    public static readonly DependencyProperty IsSourceProperty = DependencyProperty.RegisterAttached(
        "IsSource", typeof(bool), typeof(BackdropCapture), new PropertyMetadata(false));

    public static void SetIsSource(DependencyObject element, bool value) => element.SetValue(IsSourceProperty, value);
    public static bool GetIsSource(DependencyObject element) => (bool)element.GetValue(IsSourceProperty);

    #endregion

    /// <summary>
    /// 抓取 <paramref name="element"/> 所在位置的背景。
    /// 优先使用祖先链上被 <c>GlassBackdrop.IsSource=True</code> 标记的元素；
    /// 否则退化为窗口内容层（此时玻璃自身也会被捕获，请尽量标记内容层以避免反馈）。
    /// </summary>
    public static BitmapSource? Capture(FrameworkElement element, double scale = 0.4)
    {
        if (element is null || !element.IsLoaded) return null;
        var width = element.ActualWidth;
        var height = element.ActualHeight;
        if (width < 2 || height < 2) return null;

        var source = FindSource(element);
        if (source is null) return null;

        var root = source;
        var pixelWidth = Math.Max(1, (int)Math.Round(root.ActualWidth * scale));
        var pixelHeight = Math.Max(1, (int)Math.Round(root.ActualHeight * scale));
        if (pixelWidth < 2 || pixelHeight < 2) return null;

        var now = Environment.TickCount64;
        var cached = Cache.TryGetValue(root, out var entry) && entry is not null
                     && now - entry.Stamp < 16
                     && entry.PixelWidth == pixelWidth
                     && entry.PixelHeight == pixelHeight;
        if (cached) return Crop(entry!.Bitmap, element, root, scale) ?? entry.Bitmap;

        try
        {
            var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(root);
            var bmp = new WriteableBitmap(rtb); // 冻结一份，避免后续 render 干扰
            bmp.Freeze();
            Cache[root] = new Entry { Bitmap = bmp, Stamp = now, PixelWidth = pixelWidth, PixelHeight = pixelHeight };
            return Crop(bmp, element, root, scale) ?? bmp;
        }
        catch
        {
            // 远程桌面 / 软件渲染 / 最小化窗口下可能失败，静默降级为纯色调玻璃
            return null;
        }
    }

    private static FrameworkElement? FindSource(FrameworkElement element)
    {
        // 1) 祖先链优先：把玻璃放进"内容层"里的常规写法
        var current = (DependencyObject?)VisualTreeHelper.GetParent(element);
        var fallback = (FrameworkElement?)null;
        while (current is not null)
        {
            if (current is FrameworkElement fe)
            {
                if (GetIsSource(fe)) return fe;
                if (fallback is null && fe is not GlassSurface) fallback = fe;
            }
            current = VisualTreeHelper.GetParent(current);
        }

        // 2) 兄弟层：玻璃浮在内容层之上时，内容层不是祖先而是兄弟。
        //    此时必须找到被标记的那一层，否则会把玻璃自己拍进去，产生"自我捕获"的拖影/散射。
        var win = Window.GetWindow(element);
        if (win?.Content is FrameworkElement content)
        {
            var marked = FindMarkedDescendant(content);
            if (marked is not null) return marked;
            return content;
        }

        return fallback;
    }

    /// <summary>广度优先找第一个 IsSource=True 的后代。</summary>
    private static FrameworkElement? FindMarkedDescendant(DependencyObject root)
    {
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (node is FrameworkElement fe && fe != root && GetIsSource(fe)) return fe;
            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++) queue.Enqueue(VisualTreeHelper.GetChild(node, i));
        }
        return null;
    }

    /// <summary>把整张捕获位图裁到玻璃元素的矩形上。</summary>
    private static BitmapSource? Crop(BitmapSource? bitmap, FrameworkElement element, FrameworkElement root, double scale)
    {
        if (bitmap is null) return null;
        try
        {
            var offset = element.TransformToAncestor(root).Transform(new Point(0, 0));
            var x = Math.Max(0, (int)Math.Round(offset.X * scale));
            var y = Math.Max(0, (int)Math.Round(offset.Y * scale));
            var w = Math.Min(bitmap.PixelWidth - x, (int)Math.Round(element.ActualWidth * scale));
            var h = Math.Min(bitmap.PixelHeight - y, (int)Math.Round(element.ActualHeight * scale));
            if (w < 2 || h < 2) return bitmap;

            var cropped = new CroppedBitmap(bitmap, new Int32Rect(x, y, w, h));
            cropped.Freeze();
            return cropped;
        }
        catch
        {
            return bitmap;
        }
    }

    /// <summary>平均亮度（0..1），用于明暗自适应判断。</summary>
    public static double AverageLuminance(BitmapSource? bitmap)
    {
        if (bitmap is null || bitmap.PixelWidth == 0 || bitmap.PixelHeight == 0) return 0.5;
        try
        {
            var fmt = bitmap.Format;
            if (fmt != PixelFormats.Bgra32 && fmt != PixelFormats.Pbgra32)
                bitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);

            var w = Math.Min(24, bitmap.PixelWidth);
            var h = Math.Min(24, bitmap.PixelHeight);
            var stride = w * 4;
            var buffer = new byte[stride * h];
            bitmap.CopyPixels(new Int32Rect(0, 0, w, h), buffer, stride, 0);

            double sum = 0;
            for (var i = 0; i < buffer.Length; i += 4)
            {
                var b = buffer[i] / 255.0;
                var g = buffer[i + 1] / 255.0;
                var r = buffer[i + 2] / 255.0;
                sum += 0.2126 * r + 0.7152 * g + 0.0722 * b;
            }
            return sum / (w * h);
        }
        catch
        {
            return 0.5;
        }
    }

    /// <summary>清空缓存（例如切换壁纸后）。</summary>
    public static void Invalidate() => Cache.Clear();
}

