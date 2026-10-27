Shader "Hidden/WeatherPuddlePlanar"
{
    Properties
    {
        _ReflTex ("Planar Reflection", 2D) = "black" {}
        _Water ("Water Color", Color) = (0.025, 0.035, 0.045, 1)
        _Strength ("Reflection Strength", Range(0,1)) = 0.6
        _Blur ("Blur (mip level)", Range(0,6)) = 2.2
        _Distort ("Ripple Distortion", Range(0,0.1)) = 0.012
        _Wet ("Wetness", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -2, -2
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ReflTex);
            SAMPLER(sampler_ReflTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Water;
                half _Strength;
                half _Blur;
                half _Distort;
                half _Wet;
            CBUFFER_END

            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 cs : SV_POSITION; float2 uv : TEXCOORD0; float4 ndc : TEXCOORD1; float3 wp : TEXCOORD2; };

            V vert(A i)
            {
                V o;
                VertexPositionInputs vp = GetVertexPositionInputs(i.pos.xyz);
                o.cs = vp.positionCS;
                o.ndc = vp.positionNDC;
                o.wp = vp.positionWS;
                o.uv = i.uv;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float t = _Time.y;
                float2 p = i.wp.xz;
                float2 rip = float2(sin(p.x * 7.0 + t * 1.3) + sin(p.y * 9.0 - t * 1.1),
                                    sin(p.y * 6.0 + t * 1.2) + sin(p.x * 8.0 + t * 0.9)) * 0.5;
                float2 suv = i.ndc.xy / i.ndc.w + rip * _Distort;
                half3 refl = SAMPLE_TEXTURE2D_LOD(_ReflTex, sampler_ReflTex, suv, _Blur).rgb;
                float3 V3 = normalize(_WorldSpaceCameraPos - i.wp);
                float fres = lerp(0.18, 1.0, pow(1.0 - saturate(V3.y), 4.0));
                half3 col = lerp(_Water.rgb, refl, saturate(fres * _Strength));
                float edge = 1.0 - smoothstep(0.72, 1.0, length(i.uv - 0.5) * 2.0);
                return half4(col, saturate(edge * (0.55 + 0.4 * _Wet)));
            }
            ENDHLSL
        }
    }
}
