// 塔罗形态调色。逐纹素判断，点采样，不做模糊，也不在相邻颜色之间插值。
// _FormId = 0 原样输出；1 愚者；2 战车；3 力量。死亡、月亮先原样输出。
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

        // 战车：深海军蓝、银、皇家蓝、金。皮肤和描边不动。眼睛由 EyeGuarded 留在原蓝。
        float3 RemapChariot(float3 rgb, float alpha)
        {
            if (alpha <= 0.004)
                return rgb;

            float3 hsv = TarotRgbToHsv(rgb);
            float hue = hsv.x * 360.0;
            float sat = hsv.y;
            float val = hsv.z;

            // 脸和皮肤：偏红的暖色，不参与换色。
            bool skinLike = hue <= 36.0 && rgb.r > rgb.g + 0.03 && val > 0.55 && sat < 0.5;
            if (skinLike)
                return rgb;

            // 描边、闭着的眼线：很暗且不是蓝，保持黑色。
            if (val < 0.18 && !(hue >= 190.0 && hue <= 255.0 && sat >= 0.25))
                return rgb;

            bool warm = (hue <= 70.0 || hue >= 330.0) && sat >= 0.045 && val >= 0.12;
            if (warm)
            {
                // 最亮的金点留一点金，其余金属、杖身、帽饰改成银。
                if (sat >= 0.5 && val >= 0.82 && hue <= 58.0)
                    return TarotHsvToRgb(46.0, min(0.7, sat), val);
                return TarotHsvToRgb(214.0, 0.07, lerp(0.32, 0.9, saturate(val)));
            }

            bool cool = (hue >= 160.0 && hue <= 275.0 && sat >= 0.025) || (sat < 0.10 && (rgb.b - rgb.r) > 0.012 && val > 0.2);
            if (!cool)
            {
                // 几乎纯白的衣料留白，略冷的浅色收成皇家蓝，形成分层。
                if (sat < 0.06 && val > 0.9)
                    return lerp(rgb, float3(0.94, 0.96, 0.99), 0.4);
                if (val > 0.78 && sat < 0.16)
                    return TarotHsvToRgb(222.0, 0.42, lerp(0.72, 0.9, saturate(val)));
                return rgb;
            }

            // 法杖上很亮的蓝：发光的星晶蓝，不改成金。
            if (sat >= 0.55 && val >= 0.72 && hue >= 185.0 && hue <= 240.0)
                return TarotHsvToRgb(198.0, 0.7, min(1.0, 0.82 + 0.18 * val));

            // 银白头发：亮部银蓝，暗部深海军蓝。
            if (sat < 0.18)
            {
                if (val > 0.86)
                    return TarotHsvToRgb(208.0, 0.18, min(1.0, val));
                if (val > 0.68)
                    return TarotHsvToRgb(216.0, 0.32, val * 0.82);
                return TarotHsvToRgb(222.0, 0.68, val * 0.5 + 0.08);
            }

            // 帽子、衣服、斗篷外侧的蓝：浅处皇家蓝，深处海军蓝。
            float depth = saturate(1.0 - val);
            return TarotHsvToRgb(222.0, lerp(0.48, 0.78, depth), lerp(0.74, 0.2, depth));
        }

        // 力量：奶油白、深绯红、暖金、蜂蜜玫瑰棕。皮肤和描边不动。眼睛由 EyeGuarded 留在原蓝。
        float3 RemapStrength(float3 rgb, float alpha)
        {
            if (alpha <= 0.004)
                return rgb;

            float3 hsv = TarotRgbToHsv(rgb);
            float hue = hsv.x * 360.0;
            float sat = hsv.y;
            float val = hsv.z;

            bool skinLike = hue <= 36.0 && rgb.r > rgb.g + 0.03 && val > 0.55 && sat < 0.5;
            if (skinLike)
                return rgb;

            if (val < 0.18 && !(hue >= 190.0 && hue <= 255.0 && sat >= 0.25))
                return rgb;

            bool warm = (hue <= 70.0 || hue >= 330.0) && sat >= 0.045 && val >= 0.12;
            if (warm)
            {
                // 最亮的金点留金。杖身、金属、帽饰改成暖金。
                if (sat >= 0.5 && val >= 0.82 && hue <= 58.0)
                    return TarotHsvToRgb(42.0, min(0.72, sat), val);
                return TarotHsvToRgb(36.0, 0.48, lerp(0.42, 0.9, saturate(val)));
            }

            bool cool = (hue >= 160.0 && hue <= 275.0 && sat >= 0.025) || (sat < 0.10 && (rgb.b - rgb.r) > 0.012 && val > 0.2);
            if (!cool)
            {
                // 几乎纯白 → 奶油白。略冷的浅色 → 暖象牙，和奶油白分层。
                if (sat < 0.06 && val > 0.9)
                    return TarotHsvToRgb(40.0, 0.1, min(1.0, val));
                if (val > 0.78 && sat < 0.16)
                    return TarotHsvToRgb(28.0, 0.22, lerp(0.78, 0.94, saturate(val)));
                return rgb;
            }

            // 法杖蓝珠改成暖金琥珀光，不再留蓝，免得和战车的星晶蓝混在一起。
            if (sat >= 0.55 && val >= 0.72 && hue >= 185.0 && hue <= 240.0)
                return TarotHsvToRgb(38.0, 0.72, min(1.0, 0.8 + 0.2 * val));

            // 银白头发：亮部玫瑰金，暗部蜂蜜玫瑰棕。
            if (sat < 0.18)
            {
                if (val > 0.86)
                    return TarotHsvToRgb(32.0, 0.38, min(1.0, val));
                if (val > 0.68)
                    return TarotHsvToRgb(24.0, 0.42, val * 0.78);
                return TarotHsvToRgb(18.0, 0.48, val * 0.55 + 0.12);
            }

            // 帽子、蝴蝶结、衣服上的蓝：浅处亮绯红，深处深绯红。
            float depth = saturate(1.0 - val);
            return TarotHsvToRgb(352.0, lerp(0.55, 0.78, depth), lerp(0.72, 0.24, depth));
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
            // 愚者（1）、战车（2）、力量（3）换色。死亡、月亮先原样画。
            [branch]
            if (_FormId > 0.5 && _FormId < 1.5)
            {
                if (alpha > 0.004 && !EyeGuarded(input.uv, srgb, alpha))
                    srgb = RemapFool(srgb, alpha);
            }
            else if (_FormId > 1.5 && _FormId < 2.5)
            {
                if (alpha > 0.004 && !EyeGuarded(input.uv, srgb, alpha))
                    srgb = RemapChariot(srgb, alpha);
            }
            else if (_FormId > 2.5 && _FormId < 3.5)
            {
                if (alpha > 0.004 && !EyeGuarded(input.uv, srgb, alpha))
                    srgb = RemapStrength(srgb, alpha);
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
