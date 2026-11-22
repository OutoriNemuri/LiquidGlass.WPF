using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LiquidGlass.WPF.Theme;

namespace LiquidGlass.WPF.Animation;

/// <summary>
/// 阻尼弹簧：iOS 的 .spring(duration:bounce:) 在数学上是一次二阶阻尼系统
/// ζ = 1 - bounce，ω = 2π / duration。这里用固定子步长积分，支持中途改目标并保留速度，
/// 因此可以做出「胶囊被手指拖着走、松手后弹回」的效果。
/// </summary>
public sealed class Spring
{
    private const double SubStep = 1.0 / 240.0;

    private readonly double _omega;
    private readonly double _zeta;

    public double Value { get; private set; }
    public double Velocity { get; private set; }
    public double Target { get; set; }
    public bool IsSettled { get; private set; }

    public Spring(double duration, double bounce, double initial = 0)
    {
        var d = Math.Max(0.05, duration);
        _omega = 2 * Math.PI / d;
        _zeta = Math.Clamp(1 - Math.Clamp(bounce, 0, 1), 0.02, 1.6);
        Value = initial;
        Target = initial;
        IsSettled = true;
    }

    public Spring(SpringPreset preset, double initial = 0) : this(preset.Duration, preset.Bounce, initial) { }

    /// <summary>推进 dt 秒（内部自动切成 1/240 子步长，保证不同帧率下手感一致）。</summary>
    public void Step(double dt)
    {
        if (IsSettled && Math.Abs(Value - Target) < 1e-4) return;
        var remaining = Math.Max(0, dt);
        while (remaining > 0)
        {
            var h = Math.Min(SubStep, remaining);
            var accel = -_omega * _omega * (Value - Target) - 2 * _zeta * _omega * Velocity;
            Velocity += accel * h;
            Value += Velocity * h;
            remaining -= h;
        }
        if (Math.Abs(Value - Target) < 0.0005 && Math.Abs(Velocity) < 0.02)
        {
            Value = Target;
            Velocity = 0;
            IsSettled = true;
        }
        else
        {
            IsSettled = false;
        }
    }

    public void Poke(double velocity) { Velocity += velocity; IsSettled = false; }

    /// <summary>
    /// 改目标并重新唤醒：直接给 Target 赋值不会清除 IsSettled 标记，
    /// 静止状态下改目标必须走这里，否则动画永远不会启动。
    /// </summary>
    public void Retarget(double target)
    {
        Target = target;
        if (Math.Abs(Value - target) > 1e-4) IsSettled = false;
    }
}

/// <summary>
/// 解析式弹簧缓动，可直接塞进 XAML 的 DoubleAnimation：
/// <c>&lt;DoubleAnimation EasingFunction="{lg:SpringEase DurationSeconds=0.42, Bounce=0.11}" /&gt;</c>
/// 保留 TimedAnimation 的 Duration 语义，但曲线是真实的弹簧阶跃响应（允许 overshoot）。
/// </summary>
public sealed class SpringEase : IEasingFunction
{
    public double DurationSeconds { get; set; } = 0.42;
    public double Bounce { get; set; } = 0.11;

    public double Ease(double normalizedTime)
    {
        if (normalizedTime <= 0) return 0;
        if (normalizedTime >= 1) return 1;
        var zeta = Math.Clamp(1 - Math.Clamp(Bounce, 0, 1), 0.02, 1.6);
        var omega = 2 * Math.PI / Math.Max(0.05, DurationSeconds);
        var t = normalizedTime * Math.Max(0.05, DurationSeconds);
        var omegaD = omega * Math.Sqrt(Math.Max(1e-6, 1 - zeta * zeta));
        var envelope = Math.Exp(-zeta * omega * t);
        var value = 1 - envelope * (Math.Cos(omegaD * t) + (zeta * omega / omegaD) * Math.Sin(omegaD * t));
        return value;
    }
}

/// <summary>
/// 一个极小的补间引擎：不创建 Storyboard，直接用 CompositionTarget.Rendering 驱动回调，
/// 便于在拖动 / 手势期间随时改目标（Storyboard 做不到这一点）。
/// </summary>
public static class SpringAnimator
{
    private sealed class Entry
    {
        public Spring Spring = null!;
        public Action<double> OnUpdate = null!;
        public Action? OnComplete;
        public long LastTicks;
    }

    private static readonly List<Entry> Active = new();
    private static bool _hooked;

    public static void Animate(
        double from,
        double to,
        SpringPreset preset,
        Action<double> onUpdate,
        Action? onComplete = null,
        double? initialVelocity = null)
    {
        var spring = new Spring(preset, from) { Target = to };
        if (initialVelocity is { } v) spring.Poke(v);
        var entry = new Entry
        {
            Spring = spring,
            OnUpdate = onUpdate,
            OnComplete = onComplete,
            LastTicks = Environment.TickCount64,
        };
        Active.Add(entry);
        onUpdate(from);
        Hook();
    }

    /// <summary>取消某个回调对应的动画（例如控件被卸载）。</summary>
    public static void Cancel(Action<double> onUpdate)
    {
        Active.RemoveAll(e => e.OnUpdate.Equals(onUpdate));
    }

    private static void Hook()
    {
        if (_hooked) return;
        _hooked = true;
        CompositionTarget.Rendering += OnRendering;
    }

    private static void OnRendering(object? sender, EventArgs e)
    {
        var now = Environment.TickCount64;
        for (var i = Active.Count - 1; i >= 0; i--)
        {
            var entry = Active[i];
            var dt = Math.Min(0.05, (now - entry.LastTicks) / 1000.0);
            entry.LastTicks = now;
            entry.Spring.Step(dt);
            entry.OnUpdate(entry.Spring.Value);
            if (entry.Spring.IsSettled)
            {
                Active.RemoveAt(i);
                entry.OnComplete?.Invoke();
            }
        }
        if (Active.Count == 0 && _hooked)
        {
            CompositionTarget.Rendering -= OnRendering;
            _hooked = false;
        }
    }
}

/// <summary>把「按下缩放 + 从指尖扩散的光晕」封成一个可复用的行为。</summary>
public sealed class PressFeedback
{
    private readonly FrameworkElement _target;
    private readonly Spring _spring;
    private readonly ScaleTransform _scale = new(1, 1);

    public double Glow { get; private set; }

    public PressFeedback(FrameworkElement target, SpringPreset? preset = null)
    {
        _target = target;
        _scale.CenterX = double.NaN;
        _scale.CenterY = double.NaN;
        if (_target.RenderTransform is not ScaleTransform) _target.RenderTransform = _scale;
        _spring = new Spring(preset ?? GlassTokens.Current.SpringPress, 1.0);
        _spring.Target = 1.0;
    }

    public void Press(double x, double y)
    {
        _scale.CenterX = x;
        _scale.CenterY = y;
        Glow = 1.0;
        _spring.Target = GlassTokens.Current.PressScale;
        _spring.Poke(-0.6);
    }

    public void Release()
    {
        Glow = 0;
        _spring.Target = 1.0;
        _spring.Poke(0.9);
    }

    public void Step(double dt)
    {
        _spring.Step(dt);
        _scale.ScaleX = _spring.Value;
        _scale.ScaleY = _spring.Value;
    }
}
