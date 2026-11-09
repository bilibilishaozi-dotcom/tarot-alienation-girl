// 愚者形态调色。规则与 Tools/tarot_palette_preview.py 保持一致。
// 逐纹素判断，点采样，不做模糊，也不在相邻颜色之间插值。
// _FormId = 0 原样输出；1 愚者；其它值（死亡、月亮）先原样输出。
Shader "Tarot/PaletteSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FormId ("Form Id", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
            "PreviewType" = "Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

        TEXTURE2D(_MainTex);
        float4 _MainTex_ST;
        float4 _MainTex_TexelSize;
        float4 _Color;
        float4 _RendererColor;
        float _FormId;

        // 和预览脚本一样：游戏帧大约 216 像素高，眼睛周围看 6 像素。
        #define EYE_RADIUS 6
        #define EYE_SKIN_NEED 28.0

        struct Attributes
        {
            float3 positionOS : POSITION;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
        };

        float3 TarotRgbToHsv(float3 c)
        {
            float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
            float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
            float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
            float d = q.x - min(q.w, q.y);
            float e = 1.0e-10;
            return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
        }

        float3 TarotHsvToRgb(float hDeg, float s, float v)
        {
            // 与 Python colorsys.hsv_to_rgb 一致（Unity Color.hlsl 的写法）。
            float h = frac(hDeg / 360.0);
            s = saturate(s);
            v = saturate(v);
            float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
            float3 p = abs(frac(h + K.xyz) * 6.0 - K.www);
            return v * lerp(K.xxx, saturate(p - K.xxx), s);
        }

        float3 SampleSrgb(float2 uv, out float alpha)
        {
            float4 texel = _MainTex.SampleLevel(sampler_PointClamp, uv, 0);
            alpha = texel.a;
            return LinearToSRGB(texel.rgb);
        }

        bool IsSkin(float3 rgb, float alpha)
        {
            if (alpha < 0.70)
                return false;
            if (!(rgb.r > rgb.g + 0.035 && rgb.g + 0.02 >= rgb.b && rgb.r > 0.72 && rgb.b < 0.92))
                return false;
            float3 hsv = TarotRgbToHsv(rgb);
            float hue = hsv.x * 360.0;
            return hue >= 4.0 && hue <= 30.0 && hsv.y >= 0.07 && hsv.y <= 0.36 && hsv.z >= 0.70;
        }

        bool IsEyeCore(float3 rgb, float alpha)
        {
            if (alpha < 0.55)
                return false;
            float3 hsv = TarotRgbToHsv(rgb);
            float hue = hsv.x * 360.0;
            return hue >= 185.0 && hue <= 235.0 && hsv.y >= 0.42 && hsv.z >= 0.45;
        }

        bool IsCountBlue(float3 rgb, float alpha)
        {
            if (alpha < 0.30)
                return false;
            float3 hsv = TarotRgbToHsv(rgb);
            float hue = hsv.x * 360.0;
            return hue >= 180.0 && hue <= 250.0 && hsv.y >= 0.35 && hsv.z >= 0.35;
        }

        bool IsBlueish(float3 rgb, float alpha)
        {
            if (alpha < 0.25)
                return false;
            float3 hsv = TarotRgbToHsv(rgb);
            float hue = hsv.x * 360.0;
            return hue >= 170.0 && hue <= 255.0 && hsv.y >= 0.12 && hsv.z >= 0.20;
        }

        // 眼睛和法杖宝珠是同一种蓝。宝珠周围是金属和空白，眼睛周围是皮肤。
        bool NeighborhoodIsEye(float2 uv)
        {
            float2 texel = _MainTex_TexelSize.xy;
            float skin = 0.0;
            float blue = 0.0;
            [loop]
            for (int j = -EYE_RADIUS; j <= EYE_RADIUS; j++)
            {
                [loop]
                for (int i = -EYE_RADIUS; i <= EYE_RADIUS; i++)
                {
                    float2 suv = uv + float2(i, j) * texel;
                    if (suv.x < 0.0 || suv.y < 0.0 || suv.x > 1.0 || suv.y > 1.0)
                        continue;

                    float alpha;
                    float3 rgb = SampleSrgb(suv, alpha);
                    if (IsSkin(rgb, alpha))
                        skin += 1.0;
                    if (IsCountBlue(rgb, alpha))
                        blue += 1.0;
                }
            }

            return skin >= EYE_SKIN_NEED && skin >= blue * 0.90;
        }

        bool EyeGuarded(float2 uv, float3 rgb, float alpha)
        {
            if (IsEyeCore(rgb, alpha))
                return NeighborhoodIsEye(uv);

            if (!IsBlueish(rgb, alpha))
                return false;

            // 往外扩 1 像素，避免眼睛边缘被换成一圈金边。
            float2 texel = _MainTex_TexelSize.xy;
            [unroll]
            for (int j = -1; j <= 1; j++)
            {
                [unroll]
                for (int i = -1; i <= 1; i++)
                {
                    if (i == 0 && j == 0)
                        continue;

                    float2 nuv = uv + float2(i, j) * texel;
                    if (nuv.x < 0.0 || nuv.y < 0.0 || nuv.x > 1.0 || nuv.y > 1.0)
                        continue;

                    float nAlpha;
                    float3 nRgb = SampleSrgb(nuv, nAlpha);
                    if (IsEyeCore(nRgb, nAlpha) && NeighborhoodIsEye(nuv))
                        return true;
                }
            }

            return false;
        }

        float3 RemapFool(float3 rgb, float alpha)
        {
            if (alpha <= 0.004)
                return rgb;

            float3 hsv = TarotRgbToHsv(rgb);
            float hue = hsv.x * 360.0;
            float s = hsv.y;
            float v = hsv.z;

            bool warm = (hue <= 70.0 || hue >= 330.0) && s >= 0.045;
            if (warm && v >= 0.12)
                return rgb;

            if (v < 0.18 && !(hue >= 190.0 && hue <= 255.0 && s >= 0.25))
                return rgb;

            bool cool = (hue >= 160.0 && hue <= 275.0 && s >= 0.025) || (s < 0.10 && (rgb.b - rgb.r) > 0.012 && v > 0.2);
            if (!cool)
            {
                if (s < 0.07 && v > 0.82)
                {
                    float3 cream = float3(0.973, 0.941, 0.847);
                    return lerp(rgb, cream, 0.42);
                }
                return rgb;
            }

            if (v < 0.22)
                return TarotHsvToRgb(22.0, min(0.55, 0.22 + s * 0.35), v * 0.92);

            if (s >= 0.55 && v >= 0.70 && hue >= 185.0 && hue <= 240.0)
                return TarotHsvToRgb(46.0, 0.78, min(1.0, 0.78 + 0.20 * v));

            if (s < 0.18)
            {
                float shadow = saturate((0.96 - v) / 0.55);
                float sat = min(0.40, 0.07 + 0.30 * shadow + s * 0.35);
                float hueOut = 38.0 + 6.0 * (1.0 - shadow);
                return TarotHsvToRgb(hueOut, sat, min(1.0, v * 0.99 + 0.01));
            }

            float clothShadow = saturate(1.0 - v);
            float hueOut = 34.0 + 10.0 * v;
            float sat = min(0.62, 0.22 + 0.40 * min(1.0, s) + 0.08 * clothShadow);
            float val = min(1.0, v * 0.78 + 0.16);
            return TarotHsvToRgb(hueOut, sat, val);
        }

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS);
            output.uv = TRANSFORM_TEX(input.uv, _MainTex);
            output.color = input.color * _Color * _RendererColor;
            return output;
        }

        float4 Frag(Varyings input) : SV_Target
        {
            float alpha;
            float3 srgb = SampleSrgb(input.uv, alpha);
            // HLSL 的 && 不会短路，所以换色放进真正的分支里。原版不采样周围像素。
            // 只有愚者（1）换色。死亡、月亮先原样画。
            [branch]
            if (_FormId > 0.5 && _FormId < 1.5)
            {
                if (alpha > 0.004 && !EyeGuarded(input.uv, srgb, alpha))
                    srgb = RemapFool(srgb, alpha);
            }

            float4 color = float4(SRGBToLinear(srgb), alpha) * input.color;
            return color;
        }
        ENDHLSL

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            ENDHLSL
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            ENDHLSL
        }
    }
}
