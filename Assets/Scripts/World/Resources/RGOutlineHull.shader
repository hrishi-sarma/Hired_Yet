// Inverted-hull outline (URP). Back faces are pushed out along the smoothed normal and drawn in a flat colour.
// Properties are driven by CinematicEffects: _Color (rgb + opacity), _Width (world units, grows with distance), _Fade (start, end distance).
Shader "Hidden/RGOutlineHull"
{
    Properties
    {
        _Color ("Color", Color) = (0.08, 0.06, 0.08, 0.55)
        _Width ("Width", Float) = 0.03
        _Fade ("Fade (start, end)", Vector) = (60, 160, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Width;
                float4 _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; half alpha : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 wp = TransformObjectToWorld(v.positionOS.xyz);
                float3 wn = normalize(TransformObjectToWorldNormal(v.normalOS));
                float dist = distance(wp, _WorldSpaceCameraPos);
                float w = _Width * (1.0 + saturate(dist / 100.0) * 5.0);
                wp += wn * w;
                o.positionCS = TransformWorldToHClip(wp);
                o.alpha = (half)(_Color.a * (1.0 - smoothstep(_Fade.x, _Fade.y, dist)));
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(_Color.rgb, i.alpha);
            }
            ENDHLSL
        }
    }
}
