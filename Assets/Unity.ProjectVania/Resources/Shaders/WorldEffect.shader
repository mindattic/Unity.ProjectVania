// The original's five pixel shaders (Content\Effects\*.fx, RECONSTRUCTION 6.1) as one sprite shader, one keyword per
// preset, run per sprite in the sprite texture's own coordinates exactly as SceneManager wrapped every world sprite in
// one. Core's SpriteEffect is the same arithmetic (it paints the map's cells); keep the two in step. The fragment works
// in XNA's texture coordinates (v down) and reads the nearest texel, clamped to the edge, as Core does. The sprite's
// tint is applied to every sample before the effect (BIBLE defect 19: the original's shaders dropped it). The output is
// clamped as the original's 8-bit target clamped it before blending.
Shader "ProjectVania/WorldEffect"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Burn ("filter burn", Float) = 0.01
        _Saturation ("filter saturation", Float) = 1
        _Gain ("filter r g b", Vector) = (1, 1, 1, 0)
        _Brite ("filter brite", Float) = 0
        _BloomV ("bloom v", Float) = 0.01
        _BloomA ("bloom a", Float) = 0.2
        _Horizon ("water horizon", Float) = 0.5
        _Delta ("water delta", Float) = 0
        _Theta ("water theta", Float) = 0

        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex EffectVertex
            #pragma fragment EffectFragment
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma multi_compile_local _EFFECT_FILTER _EFFECT_NEGATIVE _EFFECT_BLOOM _EFFECT_PAUSE _EFFECT_WATER

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                float4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Burn;
                float _Saturation;
                float4 _Gain;
                float _Brite;
                float _BloomV;
                float _BloomA;
                float _Horizon;
                float _Delta;
                float _Theta;
            CBUFFER_END

            static const float2 Taps[12] =
            {
                float2(-0.326212, -0.405805),
                float2(-0.840144, -0.073580),
                float2(-0.695914,  0.457137),
                float2(-0.203345,  0.620716),
                float2( 0.962340, -0.194983),
                float2( 0.473434, -0.480026),
                float2( 0.519456,  0.767022),
                float2( 0.185461, -0.893124),
                float2( 0.507431,  0.064425),
                float2( 0.896420,  0.412458),
                float2(-0.321940, -0.932615),
                float2(-0.791559, -0.597705),
            };

            Varyings EffectVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            // tex2D at an XNA texture coordinate (v down): the nearest texel, clamped, times the sprite's tint.
            float4 Sample(float2 tex, float4 tint)
            {
                uint width, height;
                _MainTex.GetDimensions(width, height);
                float2 size = float2(width, height);
                int2 texel = clamp((int2)floor(tex * size), int2(0, 0), (int2)size - 1);
                texel.y = (int)size.y - 1 - texel.y; // Unity's rows run bottom-up
                return LOAD_TEXTURE2D(_MainTex, texel) * tint;
            }

            float4 EffectFragment(Varyings input) : SV_Target
            {
                float2 tex = float2(input.uv.x, 1.0 - input.uv.y);
                float4 tint = input.color;

            #if defined(_EFFECT_NEGATIVE)
                // negative.fx
                float4 col = Sample(tex, tint);
                col.rgb = 1 - col.rgb;
                return saturate(col);
            #elif defined(_EFFECT_BLOOM)
                // bloom.fx
                float4 col = Sample(tex, tint);
                for (int i = 0; i < 12; i++)
                    col += Sample(tex + _BloomV * Taps[i], tint);
                col /= 13.0;
                col.a = _BloomA;
                return saturate(col);
            #elif defined(_EFFECT_PAUSE)
                // pause.fx
                float4 col = 0;
                for (int i = 0; i < 12; i++)
                    col += Sample(tex + 0.005 * Taps[i], tint);
                float a = (col.r + col.g + col.b) / 3.0;
                a /= 12.0;
                col.rgb = a;
                return saturate(col);
            #elif defined(_EFFECT_WATER)
                // water.fx
                float4 sum = 0;
                float2 t = tex;
                if (t.y < 1.0)
                {
                    t.y = _Horizon - t.y;
                    t.y += (cos(t.x * 50.0 + _Delta) / 500.0);
                    t.y += (sin(t.y * 250.0 + _Theta) / 120.0);
                    t.x += (t.y * (sin(t.y * 750.0 + _Theta) / 250.0));
                    sum = Sample(t, tint);
                }
                sum *= 0.35;
                if (tex.y < 0.2)
                    sum.a = 0.0;
                else if (tex.y < 0.25)
                    sum.a = (tex.y - 0.2) * 20.0;
                else
                    sum.a = 1.0;
                return saturate(sum);
            #else
                // filter.fx; its refractSampler was never bound, so GetDif read black and displaced nothing.
                float4 col = Sample(tex, tint);
                float d = sqrt(pow(abs(tex.x - 0.5), 2) + pow(abs(tex.y - 0.5), 2));
                col.rgb -= d * _Burn;
                float a = col.r + col.g + col.b;
                a /= 3.0;
                a *= 1.0 - _Saturation;
                col.r = (col.r * _Saturation + a) * _Gain.r;
                col.g = (col.g * _Saturation + a) * _Gain.g;
                col.b = (col.b * _Saturation + a) * _Gain.b;
                col.rgb += _Brite;
                return saturate(col);
            #endif
            }
            ENDHLSL
        }
    }
}
