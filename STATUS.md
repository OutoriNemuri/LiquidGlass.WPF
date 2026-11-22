# 当前状态

本仓库在 Windows 11 上实机验证过，CI（`windows-latest`）编译通过。

## 已验证

- 库（`LiquidGlass.WPF`）与 Demo（`LiquidGlass.Demo`）均编译通过
- Demo 可启动，token 由 `EmbeddedResource` 正确加载
- 竖向 Dock、弹簧动画、明暗自适应在实机上可用

## 与效果图的差距

`renders/` 下的图片是**按同一份 token 制作的视觉参考稿**，用于表达设计意图，
并非本仓库代码的逐像素渲染输出。评估本项目请以实机运行为准。

## 已知限制

- **只在 Windows 上可用**。WPF 不跨平台。
- **背景捕获走 CPU**：`BackdropCapture` 用 `RenderTargetBitmap` 降采样 + 回读。
  在远程桌面、虚拟显示器、软件渲染等环境下可能返回空图，此时玻璃自动降级为纯色调
  （不会崩，但也没有虚化）。
- **每块玻璃各自捕获一次**，16ms 内复用。页面上玻璃越多，开销越高。
- **圆角裁剪只取单一半径**：`RectangleGeometry(rect, r, r)`，四角不等半径时不生效。
- **不使用 `DropShadowEffect`**：它会连文字一起模糊成重影，且每块玻璃一份是主要开销，
  因此改为纯描边。

## token 中尚未被消费的字段

`tokens/liquid-glass.tokens.json` 里以下几项会被解析进 `GlassMaterialSpec`，
但当前渲染代码没有读取：

| 字段 | 说明 |
|---|---|
| `material.*.saturation` | WPF `BlurEffect` 无饱和度参数，需 `Windows.UI.Composition` 或着色器 |
| `material.*.brightness` | 同上 |
| `material.*.dispersion` | 需编译后的像素着色器 `Shaders/LensRefraction.cso` |
| `material.*.noise` | 未实现 |
| `material.*.innerGlow` | 未实现 |

这些字段保留在 token 里是为了未来切换渲染后端时无需改数据结构。

## 上报问题

请附：Windows 版本、是否远程桌面/虚拟显示器、`dotnet --info`、截图。
