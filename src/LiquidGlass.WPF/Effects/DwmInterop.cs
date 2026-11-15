using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace LiquidGlass.WPF.Effects;

/// <summary>
/// Windows 合成层接入：把所有「系统自带」的材质能力集中在这里。
/// 全部调用都是"尽力而为"——不支持的版本静默降级，绝不抛异常打断应用。
/// </summary>
public static class DwmInterop
{
    // DWMWA_* 常量（Windows 11 22H2 起支持 SYSTEMBACKDROP_TYPE）
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("user32.dll")] private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref AccentPolicyData data);

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicyData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor; // AABBGGRR
        public int AnimationId;
    }

    public enum BackdropType { Auto = 0, None = 1, Mica = 2, Acrylic = 3, Tabbed = 4 }

    /// <summary>为窗口启用系统材质（Win11 Mica/Acrylic；Win10 回退到 ACCENT_ENABLE_ACRYLICBLURBEHIND）。</summary>
    public static bool EnableSystemBackdrop(Window window, BackdropType type, Color tint)
    {
        var hwnd = GetHandle(window);
        if (hwnd == IntPtr.Zero) return false;

        try
        {
            SetImmersiveDarkMode(window, IsDark(tint));
            var t = (int)type;
            if (DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref t, sizeof(int)) == 0)
            {
                // Win11：让 WPF 背景透明，系统材质才透得出来
                window.Background = Brushes.Transparent;
                return true;
            }
        }
        catch { /* 忽略 */ }

        // Win10 1903+ 的亚克力：ACCENT_ENABLE_ACRYLICBLURBEHIND = 4
        try
        {
            int gradient = (tint.A << 24) | (tint.B << 16) | (tint.G << 8) | tint.R;
            var policy = new AccentPolicy { AccentState = 4, AccentFlags = 2, GradientColor = gradient };
            var size = Marshal.SizeOf(policy);
            var ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(policy, ptr, false);
                var data = new AccentPolicyData { Attribute = 19, Data = ptr, SizeOfData = size };
                SetWindowCompositionAttribute(hwnd, ref data);
                window.Background = Brushes.Transparent;
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Win11 圆角偏好：2 = 系统默认圆角，1 = 直角。</summary>
    public static void SetRoundedCorners(Window window, bool rounded = true)
    {
        var hwnd = GetHandle(window);
        if (hwnd == IntPtr.Zero) return;
        var pref = rounded ? 2 : 1;
        DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref pref, sizeof(int));
    }

    /// <summary>标题栏跟随深浅色。</summary>
    public static void SetImmersiveDarkMode(Window window, bool dark)
    {
        var hwnd = GetHandle(window);
        if (hwnd == IntPtr.Zero) return;
        var value = dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
    }

    /// <summary>扩展 Windows 11 的 DWM 投影到整个窗口（无边框窗口也有系统级阴影）。</summary>
    public static void ExtendFrameIntoClientArea(Window window, int margin = -1)
    {
        var hwnd = GetHandle(window);
        if (hwnd == IntPtr.Zero) return;
        var m = new Margins { Left = margin, Right = margin, Top = margin, Bottom = margin };
        try { DwmExtendFrameIntoClientArea(hwnd, ref m); } catch { }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins { public int Left, Right, Top, Bottom; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    private static IntPtr GetHandle(Window window)
    {
        try
        {
            var helper = new WindowInteropHelper(window);
            return helper.Handle;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static bool IsDark(Color c)
    {
        var lum = (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
        return lum < 0.55;
    }
}
