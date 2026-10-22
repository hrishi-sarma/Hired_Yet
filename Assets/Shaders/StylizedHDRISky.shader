Shader "Skybox/Stylized HDRI Moving Clouds"
{
    Properties
    {
        _DayTex ("Day HDRI (equirectangular)", 2D) = "black" {}
        _NightTex ("Night HDRI (equirectangular)", 2D) = "black" {}
        _DawnTex ("Dawn HDRI (optional)", 2D) = "black" {}
        _DuskTex ("Dusk HDRI (optional)", 2D) = "black" {}
        _Hour ("Hour", Range(0,24)) = 10
        _Sunrise ("Sunrise Hour", Float) = 6
        _Sunset ("Sunset Hour", Float) = 18
        _CloudOffset ("Cloud Offset", Vector) = (0,0,0,0)
        _CloudOpacity ("Cloud Opacity", Range(0,1)) = 0.42
        _CloudScale ("Cloud Scale", Range(0.1,8)) = 2.2
        _CloudCoverage ("Cloud Coverage", Range(0,1)) = 0.48
        _CloudDetail ("Cloud Detail", Range(0,1)) = 0.45
        _SunDirection ("Sun Direction", Vector) = (0,1,0,0)
        _MoonDirection ("Moon Direction", Vector) = (0,-1,0,0)
        _SunBrightness ("Sun Brightness", Float) = 1
        _SunSize ("Sun Size", Float) = 1.1
        _Exposure ("Exposure", Range(0,1)) = 1
        _StarBrightness ("Star Brightness", Float) = 0.8
        _StarsEnabled ("Stars Enabled", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _DayTex, _NightTex, _DawnTex, _DuskTex;
            float _Hour, _Sunrise, _Sunset;
            float4 _CloudOffset;
            float _CloudOpacity, _CloudScale, _CloudCoverage, _CloudDetail;
            float4 _SunDirection, _MoonDirection;
            float _SunBrightness, _SunSize, _Exposure, _StarBrightness, _StarsEnabled;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float2 DirToUV(float3 d)
            {
                d = normalize(d);
                return float2(atan2(d.x, d.z) * 0.15915494 + 0.5, asin(clamp(d.y, -1.0, 1.0)) * 0.31830989 + 0.5);
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise2(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1,0)), f.x),
                            lerp(hash21(i + float2(0,1)), hash21(i + float2(1,1)), f.x), f.y);
            }

            float fbm(float2 p)
            {
                float n = 0, a = 0.5;
                n += noise2(p) * a; p *= 2.03; a *= 0.5;
                n += noise2(p) * a; p *= 2.01; a *= 0.5;
                n += noise2(p) * a; p *= 2.02; a *= 0.5;
                n += noise2(p) * a * _CloudDetail;
                return n;
            }

            float3 SamplePano(sampler2D tex, float2 uv)
            {
                // Wrap longitude and clamp latitude to avoid sampling outside the panorama.
                uv.x = frac(uv.x);
                uv.y = saturate(uv.y);
                return tex2D(tex, uv).rgb;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float2 uv = DirToUV(dir);

                float dayBlend = smoothstep(-0.12, 0.18, sin((_Hour - 6.0) / 12.0 * 3.14159265));
                float nightBlend = 1.0 - dayBlend;

                // Dawn/dusk windows soften the main day/night transition.
                float dawnWindow = exp(-pow((_Hour - _Sunrise) / 1.35, 2.0));
                float duskWindow = exp(-pow((_Hour - _Sunset) / 1.35, 2.0));

                float3 dayCol = SamplePano(_DayTex, uv);
                float3 nightCol = SamplePano(_NightTex, uv);
                float3 dawnCol = SamplePano(_DawnTex, uv);
                float3 duskCol = SamplePano(_DuskTex, uv);

                float3 col = lerp(nightCol, dayCol, dayBlend);
                col = lerp(col, dawnCol, saturate(dawnWindow * 0.9));
                col = lerp(col, duskCol, saturate(duskWindow * 0.9));

                // Animated procedural cloud layer, independent from the HDRI panorama.
                float cloudBand = smoothstep(-0.06, 0.42, dir.y) * (1.0 - smoothstep(0.72, 0.96, dir.y));
                float2 cloudUV = float2(uv.x * 5.0, uv.y * 2.8) * _CloudScale;
                cloudUV += _CloudOffset.xy;
                float cloudNoise = fbm(cloudUV);
                float cloudMask = smoothstep(_CloudCoverage, _CloudCoverage + 0.24, cloudNoise);
                float cloudDaylight = lerp(0.42, 1.0, dayBlend);
                float3 cloudTint = lerp(float3(0.58,0.66,0.82), float3(1.0,0.98,0.94), dayBlend);
                cloudTint = lerp(cloudTint, float3(1.0,0.55,0.48), saturate(dawnWindow + duskWindow));
                col = lerp(col, cloudTint, cloudMask * cloudBand * _CloudOpacity * cloudDaylight);

                // Sun disc and soft halo are aligned with the directional light.
                float sunDot = dot(dir, normalize(_SunDirection.xyz));
                float sunRadius = 0.99972 / max(0.35, _SunSize);
                float disc = smoothstep(sunRadius, sunRadius + 0.00018, sunDot);
                float halo = pow(saturate(sunDot), 180.0) * 0.32;
                col += float3(1.0, 0.78, 0.48) * (disc * _SunBrightness + halo * _SunBrightness) * dayBlend;

                // A small moon disc and cool halo, opposite the sun.
                float moonDot = dot(dir, normalize(_MoonDirection.xyz));
                float moonDisc = smoothstep(0.99955, 0.99972, moonDot);
                float moonHalo = pow(saturate(moonDot), 110.0) * 0.12;
                col += float3(0.68, 0.78, 1.0) * (moonDisc * 0.75 + moonHalo) * nightBlend;

                // Lightweight procedural stars, primarily visible at night.
                float2 starCell = floor(float2(uv.x * 520.0, uv.y * 250.0));
                float starRnd = hash21(starCell);
                float star = step(0.9978, starRnd) * smoothstep(0.05, 0.35, dir.y);
                float twinkle = 0.65 + 0.35 * sin(_Time.y * (1.3 + starRnd * 3.0) + starRnd * 80.0);
                col += star * twinkle * _StarBrightness * _StarsEnabled * nightBlend;

                // Occasional animated shooting star: a short diagonal streak crossing the sky.
                float shootPhase = frac(_Time.y * 0.035);
                float shootActive = step(0.80, hash21(float2(floor(_Time.y * 0.035), 17.7)));
                float2 shootUV = uv - float2(frac(shootPhase * 1.8) * 0.85 + 0.05,
                                             0.72 - shootPhase * 0.48);
                float streakAxis = abs(shootUV.y + shootUV.x * 0.42);
                float streakLength = smoothstep(0.12, 0.0, abs(shootUV.x)) *
                                     smoothstep(0.018, 0.002, streakAxis);
                float streak = streakLength * shootActive * step(0.82, nightBlend);
                col += float3(0.72, 0.84, 1.0) * streak * 1.8;

                col *= _Exposure;
                return fixed4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
