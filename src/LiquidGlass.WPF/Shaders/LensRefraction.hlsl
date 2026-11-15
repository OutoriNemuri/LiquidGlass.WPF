// ─────────────────────────────────────────────────────────────
// Liquid Glass · 边缘透镜像素着色器（可选）
//
// fxc /T ps_2_0 /E main /Fo LensRefraction.cso LensRefraction.hlsl
//
// 作用：在圆角内侧 2~3px 的环带里，按"球面法线"偏移采样坐标，
// 让背景产生真实的边缘弯折与 R/B 通道错位（色散）。
// 未提供 .cso 时，GlassSurface 会退化为"整块放大"的几何近似。
// ─────────────────────────────────────────────────────────────

sampler2D SceneSampler : register(s0);

// 纹理像素尺寸（1/宽, 1/高）
float2 TexelSize;
// 圆角半径（像素）与透镜强度
float  Radius;
float  LensScale;
float  Dispersion;

float4 main(float2 uv : TEXCOORD0) : COLOR
{
    // 到圆角中心的归一化距离（以左上角圆心为参考，实际使用时请把四个圆心拼成 SDF）
    float2 r = float2(Radius, Radius);
    float2 c = float2(clamp(uv.x * TexelSize.x, r.x, 1.0 - r.x),
                     clamp(uv.y * TexelSize.y, r.y, 1.0 - r.y));
    float2 d = (uv - c);
    float  len = length(d * float2(1.0 / Radius, 1.0 / Radius));
    float  band = saturate(1.0 - len);          // 1 = 圆角内侧，0 = 矩形中心

    // 环带内的球面法线 → 采样位移（越靠边弯折越强）
    float2 n = normalize(d + 1e-5);
    float  k = band * band * (LensScale - 1.0) * Radius;

    float2 uvR = uv - n * k * (1.0 + Dispersion);
    float2 uvG = uv - n * k;
    float2 uvB = uv - n * k * (1.0 - Dispersion);

    float rCh = tex2D(SceneSampler, uvR).r;
    float gCh = tex2D(SceneSampler, uvG).g;
    float bCh = tex2D(SceneSampler, uvB).b;

    return float4(rCh, gCh, bCh, 1.0);
}
