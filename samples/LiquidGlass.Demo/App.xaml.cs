using System;
using System.Windows;
using LiquidGlass.WPF.Theme;

namespace LiquidGlass.Demo;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1) 把 token 编译成资源字典（颜色 / 圆角 / 动效 / 字号）
        GlassTheme.Install(this);

        // 2) 跟随 Windows 的浅色/深色设置；也可强制 GlassThemeMode.Light / Dark
        GlassTheme.Mode = GlassThemeMode.System;

        // 3) 尊重辅助功能开关
        GlassTheme.ReduceTransparency = false;
        GlassTheme.IncreaseContrast = false;
    }
}
