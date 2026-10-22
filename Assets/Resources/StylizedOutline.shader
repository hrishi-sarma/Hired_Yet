Shader "Stylized/InvertedHullOutline"
{
    Properties
    {
        _Color ("Color", Color) = (0.08, 0.06, 0.08, 0.55)
        _Width ("Width", Float) = 0.03
        _Fade ("Fade Start/End", Vector) = (60, 160, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-50" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="UniversalForward" }
            Cull Front
            ZWrite Off
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
            struct Varyings { float4 positionCS : SV_POSITION; float depth : TEXCOORD0; };
            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 wp = TransformObjectToWorld(i.positionOS.xyz);
                float3 wn = normalize(TransformObjectToWorldNormal(i.normalOS));
                float depth = length(GetCameraPositionWS() - wp);
                wp += wn * (_Width * clamp(depth / 20.0, 0.5, 6.0));
                o.positionCS = TransformWorldToHClip(wp);
                o.depth = depth;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float f = 1.0 - saturate((i.depth - _Fade.x) / max(0.001, _Fade.y - _Fade.x));
                return half4(_Color.rgb, _Color.a * f);
            }
            ENDHLSL
        }
    }
}
