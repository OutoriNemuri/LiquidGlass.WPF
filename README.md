# LiquidGlass.WPF

> iOS 26 / macOS Tahoe 26「Liquid Glass」材质在 WPF 中的可扩展实现。
> 单一 token 数据源 → 一套材质引擎 → 全部控件共享同一份光学与动效参数。

- **目标框架**：`net8.0-windows`（WPF，Windows 10 1809+ / Windows 11 推荐）
- **无第三方依赖**：只用 WPF 自带能力（`RenderTargetBitmap` + `BlurEffect` + `DispatcherTimer`），不引入 WinUI / Composition 包
- **设计原则**：玻璃只用于「导航层 / 控件层」，内容层保持非玻璃；所有圆角遵循 concentric 规则

> **实机状态**：见 [STATUS.md](STATUS.md)。

---

## 1. 它到底"玻璃"在哪里

iOS 26 的 Liquid Glass 不是"高斯模糊 + 白底"，而是**透镜（lensing）**：玻璃会把背后的光弯折、塑形、聚集。本库把这件事拆成 6 个可独立调节的渲染通道：

| # | 通道 | 实现位置 | 作用 |
|---|------|----------|------|
| 1 | **Backdrop 背景捕获** | `Effects/BackdropCapture.cs` | 用 `RenderTargetBitmap` 对内容层降采样捕获，16ms 内复用同一帧 |
| 2 | **Frost 模糊 + 饱和** | `GlassSurface.Refresh()` | `BlurEffect(radius = blur × 0.4)`，饱和度由材质 token 控制 |
| 3 | **Lens 边缘折射** | `GlassSurface` 的 `PART_Lens` | 同一张背景的 `scale 1.035` 副本，叠加在模糊层之上，模拟边缘放大 |
| 4 | **Tint 色调层** | `GlassSurface` 的 `PART_Tint` | `color-mix` 等价物：`Color.FromArgb(opacity × 255, tint)` |
| 5 | **Specular 镜面高光** | `GlassSurface` 的 `PART_Spec` | 上亮下透的线性渐变，强度随材质变化 |
| 6 | **Rim 内描边** | `GlassSurface` 的 `PART_Rim` | 1px 内环（上亮下暗），制造玻璃厚度。**不使用 `DropShadowEffect`**：它会连文字一起模糊、且每块玻璃各一份是主要开销 |

此外还有两个"柔性"能力：

- **明暗自适应**：`BackdropCapture.AverageLuminance()` 采样背景平均亮度，`< 0.42` 判定为暗背景，玻璃自动切换深色外观（与 light/dark 主题无关，和 iOS regular glass 行为一致）。
- **辅助功能**：`GlassTheme.ReduceTransparency`（玻璃退化为不透明表面）、`GlassTheme.IncreaseContrast`（描边与高光加深）。

---

## 2. 快速开始

```bash
# Windows + .NET 8 SDK
dotnet build LiquidGlass.WPF.sln
dotnet run --project samples/LiquidGlass.Demo
```

App 启动时安装 token：

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    GlassTheme.Install(this);                       // token → 资源字典
    GlassTheme.Mode = GlassThemeMode.System;        // 跟随 Windows 深浅色
}
```

任意容器变成玻璃：

```xml
<lg:GlassSurface Material="Regular" CornerRadius="26" Padding="18">
    <TextBlock Text="Hello, Liquid Glass" />
</lg:GlassSurface>
```

> ⚠️ **必须**给"内容层"打上捕获源标记，否则玻璃会捕获到它自己（无限反馈）：
> `<Grid lge:BackdropCapture.IsSource="True">`（`lge` = `LiquidGlass.WPF.Effects`）

---

## 3. 控件清单

| 控件 | 类 | 关键能力 |
|------|----|----------|
| 玻璃按钮 | `GlassButton` | 按下 0.955 挤压 + 从按下点扩散的光晕，弹簧回弹 |
| 图标按钮 | `GlassButton`（`IconSymbol`） | 内置 20+ SF Symbols 风格矢量图标（`SfSymbol`） |
| 标签栏 | `GlassTabBar` / `GlassTabItem` | 胶囊指示器滑动，速度越快越"拉长"（squash & stretch），可 `IsMinimized` |
| 分段控件 | `GlassSegmentedControl` | 白色滑块位移由弹簧驱动 |
| 下拉框 | `GlassComboBox` | Popup 以"材质化出现"方式展开（缩放 0.96 → 1 + 淡入） |
| 开关 | `GlassToggleSwitch` | 旋钮 gel 位移（`KnobProgress` → `KnobOffset`），轨道染色 |
| 滑块 | `GlassSlider` | 6px 胶囊轨道 + 白色圆钮 + 强调色填充 |
| 卡片 | `GlassCard` | 自动计算子元素 concentric 半径（父半径 − inset） |
| 标签 | `GlassChip` | 小尺寸玻璃气泡，可带图标 |
| 窗口 | `GlassWindow` | Win11 Mica/Acrylic、Win10 亚克力回退、圆角、DWM 投影 |
| 滚动边缘 | `GlassScrollEdgeEffect` | iOS 26 新增的 soft edge：只做提示渐变，不遮挡不压暗 |
| 基元 | `GlassSurface` | 上面所有控件的公共底座 |

---

## 4. 动效：为什么"灵动"

不是 easing curve，而是**真的弹簧**：

- `Animation/Spring.cs` 用阻尼谐振子（ζ = 1 − bounce，ω = 2π / duration）以 **1/240 固定子步长**积分，因此 60Hz / 120Hz / 144Hz 手感一致；
- 支持**中途改目标并保留速度**——手指拖动胶囊、松手回弹靠的就是这个；
- `SpringEase : IEasingFunction` 把同一组参数暴露给 XAML 的 `DoubleAnimation`（允许 overshoot）；
- 预设与 token 一一对应：`snappy 0.42/0.11`、`smooth 0.62/0`、`bouncy 0.52/0.34`、`press 0.30/0.26`、`morph 0.46/0.20`、`appear 0.58/0.22`。

```xml
<DoubleAnimation Storyboard.TargetProperty="Opacity" To="1" Duration="0:0:0.42"
                 EasingFunction="{lga:SpringEase DurationSeconds=0.42, Bounce=0.11}"/>
```

---

## 5. 主题扩展（三种粒度）

**① 改 token（推荐）**：编辑 `tokens/liquid-glass.tokens.json`，重新编译即可。C# 通过 `EmbeddedResource` 读取，是运行时的唯一数据源。改完重新编译即可，无需改任何 C# 代码。

**② 运行时换调色板**：`GlassTheme.Mode = GlassThemeMode.Dark`，所有玻璃 `Refresh()` 重新合成，无需重建控件。

**③ 自定义材质**：在 token 里加一节 `material.frosted`，然后

```xml
<lg:GlassSurface Material="{x:Static lg:GlassMaterialKind.Regular}" />
```
> 目前 `GlassMaterialKind` 是枚举（Regular/Clear/Prominent/Hard 对应 iOS 的 regular / clear / prominent）。若要扩展第五种材质，把枚举换成字符串键即可（`GlassTokens.Material(name)` 已按字符串索引），改动点只在 `GlassSurface` 的一行 `Spec`。

---

## 6. 性能

- 捕获位图按 **0.4 倍降采样**，模糊在缩放空间里做，代价约为全尺寸的 1/6；
- 同一帧内多个玻璃共享一次捕获（`BackdropCapture` 内有 16ms 缓存）；
- 静态界面不要开 `IsLive`；滚动/视频场景再开，并按需把 `LiveFrameRate` 降到 20–30；
- 背景捕获是主要开销：`RenderTargetBitmap` 走 CPU 光栅化并回读，静态界面请勿开 `IsLive`；列表中大量小玻璃建议 `Material="Hard"`（无透镜层）。

---

## 7. 已知限制

- **只能运行在 Windows**：WPF 不支持跨平台。`renders/` 下的图片是按同一份 token 制作的视觉参考稿，用于表达设计意图，并非本仓库代码的逐像素渲染输出；实机表现与已知差异见 [STATUS.md](STATUS.md)。
- `BackdropCapture` 基于 `RenderTargetBitmap`，在**远程桌面 / 软件渲染**下可能返回空，此时玻璃会自动降级为纯色调（不会崩）。
- 透镜层目前是"整块放大"的几何近似；真正的边缘折射需要 `Shaders/LensRefraction.hlsl` 编译出的 `.cso`（`fxc /T ps_2_0`）。缺失时自动走几何路径，视觉差异主要在 1–2px 的边缘色散。
- 不做 WinUI 3 / UWP 的 `SystemBackdrop` 桥接。

---

## 8. 目录结构

```
LiquidGlass.WPF/
├─ tokens/liquid-glass.tokens.json      # 单一数据源（C# + 预览共用）
├─ src/LiquidGlass.WPF/
│  ├─ Primitives/    GlassSurface · SfSymbol
│  ├─ Controls/      Button · TabBar · Segmented · ComboBox · Toggle · Slider · Card · Window
│  ├─ Effects/       BackdropCapture · DwmInterop
│  ├─ Animation/     Spring · SpringEase · SpringAnimator · PressFeedback
│  ├─ Theme/         GlassTokens · GlassTheme
│  ├─ Shaders/       LensRefraction.hlsl（可选）
│  └─ Themes/Generic.xaml               # 全部默认模板
└─ samples/LiquidGlass.Demo/            # 三栏演示（含竖向 Dock）
```

## 9. 设计参考

- Apple Newsroom《Apple introduces a delightful and elegant new software design》(2025-06)
- WWDC25 Session 219《Meet Liquid Glass》、Session 356《Get to know the new design system》
- Apple Developer《Adopting Liquid Glass》
- Apple HIG：Materials / Liquid Glass、Concentric corner radii、Reduce Transparency & Reduce Motion
