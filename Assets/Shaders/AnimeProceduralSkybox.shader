Shader "Custom/AnimeProceduralSkybox"
{
 Properties {
  _SkyColorTop("Sky Top", Color)=(0.06,0.27,0.70,1)
  _SkyColorMiddle("Sky Middle", Color)=(0.20,0.56,0.92,1)
  _SkyColorHorizon("Sky Horizon", Color)=(0.70,0.88,0.98,1)
  _SkyColorBottom("Sky Bottom", Color)=(0.30,0.45,0.62,1)
  _GradientPower("Gradient Power", Range(0.2,4))=1.25

  _CloudColorLit("Cloud Lit", Color)=(1,0.985,0.95,1)
  _CloudColorShadow("Cloud Shadow", Color)=(0.56,0.70,0.94,1)
  _CloudColorSunset("Cloud Sunset", Color)=(1,0.62,0.50,1)
  _CloudCoverage("Cloud Coverage", Range(0,1))=0.5
  _CloudScale("Cloud Scale", Range(1,30))=5.5
  _CloudDetail("Cloud Detail", Range(0,1))=0.6
  _CloudDensity("Cloud Density", Range(0.2,3))=1
  _HighCloudAmount("High Cloud Amount", Range(0,1))=0.25
  _CloudShape("Cloud Shape", Range(0.2,2))=0.85
  _CloudEdgeSoftness("Cloud Edge Softness", Range(0.001,0.25))=0.05
  _CloudSpeed("Cloud Speed", Vector)=(0.006,0.002,0,0)
  _CloudBase("Cloud Base Altitude (m)", Float)=2600
  _CloudTop("Cloud Top Altitude (m)", Float)=3400
  _CloudOpacity("Cloud Opacity", Range(0,1))=1

  _SunDirection("Sun Direction", Vector)=(0.2,0.5,0.8,0)
  _SunColor("Sun Color", Color)=(1,0.78,0.48,1)
  _SunSize("Sun Angular Radius", Range(0.001,0.05))=0.016
  _SunGlow("Sun Glow", Range(0.01,0.5))=0.055
  _SunIntensity("Sun Intensity", Range(0,4))=1.4
  _MoonDirection("Moon Direction", Vector)=(-0.2,0.5,-0.8,0)
  _MoonColor("Moon Color", Color)=(0.72,0.84,1,1)
  _MoonSize("Moon Angular Radius", Range(0.005,0.08))=0.03
  _MoonPhase("Moon Phase", Range(0.1,1))=0.82
  _MoonIntensity("Moon Intensity", Range(0,2))=0.7
  _StarIntensity("Star Intensity", Range(0,3))=1
  _StarDensity("Star Density", Range(20,500))=180
  _NightFactor("Night Factor", Range(0,1))=0
  _TimeOfDay("Time Of Day", Range(0,1))=0.35
  _ShootingStarRate("Shooting Star Rate", Range(0,8))=1.5
  _ShootingStarIntensity("Shooting Star Intensity", Range(0,4))=1.5
  _AtmosphereStrength("Atmosphere Strength", Range(0,1))=0.35
 }
 SubShader {
  Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
  Cull Off ZWrite Off
  Pass {
   Name "AnimeSky"
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.5
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

   CBUFFER_START(UnityPerMaterial)
   float4 _SkyColorTop,_SkyColorMiddle,_SkyColorHorizon,_SkyColorBottom;
   float _GradientPower;
   float4 _CloudColorLit,_CloudColorShadow,_CloudColorSunset;
   float _CloudCoverage,_CloudScale,_CloudDetail,_CloudDensity,_CloudEdgeSoftness,_HighCloudAmount,_CloudShape;
   float4 _CloudSpeed; float _CloudBase,_CloudTop,_CloudOpacity;
   float4 _SunDirection,_SunColor; float _SunSize,_SunGlow,_SunIntensity;
   float4 _MoonDirection,_MoonColor; float _MoonSize,_MoonPhase,_MoonIntensity;
   float _StarIntensity,_StarDensity,_NightFactor,_TimeOfDay,_ShootingStarRate,_ShootingStarIntensity,_AtmosphereStrength;
   CBUFFER_END

   static const int CLOUD_STEPS = 20;

   struct Attributes { float4 positionOS:POSITION; };
   struct Varyings { float4 positionHCS:SV_POSITION; float3 direction:TEXCOORD0; };

   Varyings vert(Attributes i)
   {
    Varyings o;
    o.positionHCS=TransformObjectToHClip(i.positionOS.xyz);
    o.positionHCS.z=o.positionHCS.w*UNITY_RAW_FAR_CLIP_VALUE;
    o.direction=i.positionOS.xyz;
    return o;
   }

   // ---------------------------------------------------------------- noise
   float hash21(float2 p){ float3 p3=frac(float3(p.xyx)*0.1031); p3+=dot(p3,p3.yzx+33.33); return frac((p3.x+p3.y)*p3.z); }
   float hash31(float3 p){ p=frac(p*0.1031); p+=dot(p,p.yzx+33.33); return frac((p.x+p.y)*p.z); }

   float vnoise2(float2 p){
    float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
    return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),
                lerp(hash21(i+float2(0,1)),hash21(i+float2(1,1)),f.x),f.y);
   }
   float vnoise3(float3 p){
    float3 i=floor(p), f=frac(p); f=f*f*(3-2*f);
    float n000=hash31(i), n100=hash31(i+float3(1,0,0));
    float n010=hash31(i+float3(0,1,0)), n110=hash31(i+float3(1,1,0));
    float n001=hash31(i+float3(0,0,1)), n101=hash31(i+float3(1,0,1));
    float n011=hash31(i+float3(0,1,1)), n111=hash31(i+float3(1,1,1));
    return lerp(lerp(lerp(n000,n100,f.x),lerp(n010,n110,f.x),f.y),
                lerp(lerp(n001,n101,f.x),lerp(n011,n111,f.x),f.y),f.z);
   }
   float fbm2_2(float2 p){ float v=vnoise2(p)*0.65; p=p*2.03+11.7; v+=vnoise2(p)*0.35; return v; }
   float fbm2_3(float2 p){ float v=vnoise2(p)*0.5; p=p*2.03+11.7; v+=vnoise2(p)*0.3; p=p*2.01+5.3; v+=vnoise2(p)*0.2; return v; }
   float fbm3_3(float3 p){ float v=vnoise3(p)*0.5; p=p*2.03+17.1; v+=vnoise3(p)*0.3; p=p*2.01+9.7; v+=vnoise3(p)*0.2; return v; }

   // ---------------------------------------------------------------- sky
   float3 skyGradient(float y)
   {
    float up=saturate(y);
    float3 c=lerp(_SkyColorHorizon.rgb,_SkyColorMiddle.rgb,pow(saturate(up/0.42),_GradientPower*0.6));
    c=lerp(c,_SkyColorTop.rgb,pow(saturate((up-0.25)/0.75),_GradientPower));
    return lerp(c,_SkyColorBottom.rgb,saturate(-y*3.0));
   }

   float stars(float3 d)
   {
    float3 cell=floor(d*_StarDensity); float r=hash31(cell);
    float size=lerp(0.025,0.09,hash31(cell+8.2));
    float dotShape=1-smoothstep(size*0.25,size,length(frac(d*_StarDensity)-0.5));
    return step(0.986,r)*dotShape*(0.72+0.28*sin(_Time.y*(1.2+r*3)+r*50));
   }

   float shootingStars(float3 d)
   {
    float slot=floor(_Time.y*max(0.05,_ShootingStarRate)*0.12);
    float lt=frac(_Time.y*max(0.05,_ShootingStarRate)*0.12), total=0;
    [unroll] for(int k=0;k<3;k++){
     float id=slot-k, seed=hash21(float2(id,13.7+k));
     float2 start=float2(hash21(float2(id,5.7))*2-1,lerp(0.15,0.85,hash21(float2(id,2.1))));
     float live=step(0.68,lt)*(1-step(0.92,lt)), t=saturate((lt-0.68)/0.24);
     float2 endP=start+float2(-0.34,-0.22)*t, seg=endP-start;
     float proj=saturate(dot(d.xy-start,seg)/max(dot(seg,seg),0.0001));
     float dist=length(d.xy-(start+seg*proj));
     total+=live*(1-smoothstep(0.002,0.012,dist))*(1-proj)*smoothstep(0,0.15,t)*(0.7+0.3*seed);
    }
    return saturate(total);
   }

   // ---------------------------------------------------------------- volumetric cloud layer
   // Clouds live in a flat slab [_CloudBase,_CloudTop] above the viewer. Density is a
   // coverage map (domain-warped 2D fbm) shaped by a height profile (flat bottoms, domed
   // tops) and eroded by 3D noise. Lighting is marched toward the sun/moon, then
   // quantised into soft painted bands.
   float coverageRaw(float3 pw)
   {
    float2 q=pw.xz*(_CloudScale*0.00025);
    float2 w=float2(fbm2_2(q*0.6+3.7),fbm2_2(q*0.6+9.1))-0.5;
    float2 p=q+w*1.3;
    // Large shapes dominate; the fine octave is weak so it can't spawn tiny stand-alone puffs.
    float n=vnoise2(p)*0.58; p=p*2.03+11.7; n+=vnoise2(p)*0.30; p=p*2.01+5.3; n+=vnoise2(p)*0.12;
    // Very low-frequency "cloud bank" field: clouds merge into connected systems inside
    // banks and are suppressed in the gaps between them.
    float bank=fbm2_2(q*0.22+21.3);
    return n+(bank-0.5)*0.9;
   }
   // The threshold rises with height, so each cloud is wide at the base and narrows
   // toward a rounded top (mound / cauliflower), instead of a vertical column.
   float coverageAt(float n,float hf)
   {
    float thr=lerp(0.72,0.30,_CloudCoverage)+hf*hf*0.34;
    return smoothstep(thr,thr+0.30,n);
   }
   float heightProfile(float hf)
   {
    return smoothstep(0.0,0.06,hf)*(1.0-smoothstep(0.88,1.0,hf));
   }
   float cloudDensity(float3 pw,float hf,float detailAmt)
   {
    float cov=coverageAt(coverageRaw(pw),hf);
    if(cov<0.01) return 0.0;
    float d=cov*heightProfile(hf)*1.5;
    if(d<0.02) return 0.0;
    float n=fbm3_3(pw*(_CloudScale*0.0007)+float3(0,_Time.y*0.01,0));
    d-=(1.0-n)*lerp(0.10,0.40,_CloudDetail)*lerp(0.55,1.0,hf)*detailAmt;
    return pow(saturate(d),_CloudShape);
   }
   float lightDensity(float3 pw)
   {
    float hf=(pw.y-_CloudBase)/(_CloudTop-_CloudBase);
    if(hf<0.0||hf>1.0) return 0.0;
    return coverageAt(coverageRaw(pw),hf)*heightProfile(hf)*1.4;
   }

   half4 frag(Varyings i):SV_Target
   {
    float3 dir=normalize(i.direction);
    float night=_NightFactor, day=1.0-night;
    float3 sunN=normalize(_SunDirection.xyz), moonN=normalize(_MoonDirection.xyz);
    float dawnAmt=saturate(1.0-abs(sunN.y)/0.40)*day;

    // ---------- base gradient + sunrise/sunset glow
    float3 col=skyGradient(dir.y);
    float hz=exp(-abs(dir.y)*7.0);
    float2 hd=normalize(dir.xz+1e-5), sd2=normalize(sunN.xz+1e-5);
    float side=pow(saturate(dot(hd,sd2)*0.5+0.5),3.0);
    float3 warm=lerp(float3(1,0.40,0.28),float3(1,0.72,0.50),saturate(dir.y*3+0.3));
    col+=warm*hz*dawnAmt*lerp(0.25,1.0,side)*_AtmosphereStrength*1.6;

    // ---------- night: stars
    if(night>0.02)
    {
     float horizonStar=smoothstep(-0.02,0.28,dir.y);
     col+=stars(dir)*_StarIntensity*night*horizonStar*float3(0.80,0.88,1);
    }

    // ---------- moon (painted disc, soft maria, phase, glow)
    {
     float md=dot(dir,moonN);
     float mAng=acos(clamp(md,-1.0,1.0));
     float disc=1.0-smoothstep(_MoonSize*0.92,_MoonSize,mAng);
     float3 mr=normalize(cross(float3(0,1,0),moonN)), mu=cross(moonN,mr);
     float2 muv=float2(dot(dir,mr),dot(dir,mu))/max(_MoonSize,1e-4);
     float z=sqrt(saturate(1.0-dot(muv,muv)));
     float maria=vnoise2(muv*2.6+4.0)*0.6+vnoise2(muv*6.5)*0.4;
     float3 moonCol=_MoonColor.rgb*lerp(0.70,1.08,smoothstep(0.35,0.70,maria))*(0.78+0.22*z);
     float2 sl=normalize(float2(dot(sunN,mr),dot(sunN,mu))+1e-4);
     float ph=smoothstep(-0.12,0.12,dot(muv,sl)+(_MoonPhase*2.0-1.0));
     col=lerp(col,moonCol*_MoonIntensity*1.4,disc*(0.12+0.88*ph)*night);
     float mg=1.0-md;
     col+=_MoonColor.rgb*(exp(-mg*600.0)*0.22+exp(-mg*45.0)*0.10+exp(-mg*8.0)*0.03)*night*_MoonIntensity;
    }

    // ---------- sun (disc is occluded by clouds; wide glow bleeds through them later)
    float sd=dot(dir,sunN), sg=1.0-sd;
    float sAng=acos(clamp(sd,-1.0,1.0));
    float sunDisc=1.0-smoothstep(_SunSize*0.75,_SunSize,sAng);
    float3 sunCol=_SunColor.rgb*_SunIntensity*day;
    col+=sunCol*(sunDisc*2.2+exp(-sg*260.0)*0.55);
    float3 sunWide=sunCol*(exp(-sg*40.0)*0.28+exp(-sg*8.0)*0.16+exp(-sg*2.5)*0.04);
    col+=sunWide;

    // ---------- main cumulus layer
    float dy=dir.y;
    float cloudA=0.0;
    if(dy>0.004)
    {
     float3 L=normalize(lerp(sunN,moonN,night));
     float phase=pow(saturate(dot(dir,L)*0.5+0.5),5.0);
     float3 rimCol=lerp(lerp(float3(1,1,1),_CloudColorSunset.rgb,dawnAmt),_MoonColor.rgb*0.55,night);
     float rimMul=lerp(1.0,0.35,night);

     float t0=_CloudBase/dy, t1=_CloudTop/dy;
     float stepT=(t1-t0)/CLOUD_STEPS;
     float stepEff=min(stepT,500.0);
     float detailAmt=saturate(1.0-t0/60000.0);
     float jit=hash21(floor(i.positionHCS.xy))*0.9;
     float3 wind=float3(_CloudSpeed.x,0,_CloudSpeed.y)*_Time.y*2000.0;
     float sigma=0.009*_CloudDensity;

     float3 accCol=0; float accA=0;
     [loop] for(int s=0;s<CLOUD_STEPS;s++)
     {
      float fs=(s+jit)/CLOUD_STEPS;           // == height fraction inside the slab
      float3 pw=dir*(t0+(s+jit)*stepT)+wind;
      float dens=cloudDensity(pw,fs,detailAmt);
      if(dens>0.01)
      {
       float l1=lightDensity(pw+L*90.0), l2=lightDensity(pw+L*280.0);
       float shade=exp(-(l1*0.8+l2*1.2)*1.4);
       float brush=vnoise2(pw.xz*0.008+pw.y*0.004);       // ragged painted band edges
       float sh=shade+(brush-0.5)*0.12;
       float tone=smoothstep(0.18,0.46,sh)*0.55+smoothstep(0.52,0.82,sh)*0.45;
       tone*=lerp(0.80,1.0,smoothstep(0.0,0.3,fs));

       float3 c=lerp(_CloudColorShadow.rgb,_CloudColorLit.rgb,tone);
       float under=1.0-smoothstep(0.0,0.20,fs);
       c=lerp(c,_CloudColorShadow.rgb*0.88,under*0.4*(1.0-tone*0.5));
       float edge=1.0-saturate(dens*2.2);
       c+=rimCol*edge*phase*rimMul*0.55;               // silver lining toward the light

       float a=1.0-exp(-dens*sigma*stepEff);
       accCol+=(1.0-accA)*a*c; accA+=(1.0-accA)*a;
       if(accA>0.985) break;
      }
     }

     float3 cc=accCol/max(accA,1e-3);
     float distFade=smoothstep(0.01,0.22,dy);
     cc=lerp(skyGradient(0.02),cc,lerp(0.25,1.0,distFade));   // distant clouds melt into haze
     float A=accA*smoothstep(0.005,0.09,dy);
     A=smoothstep(0.01,0.01+_CloudEdgeSoftness*8.0,A);          // soft-but-defined painted edge
     cloudA=A*_CloudOpacity;
     col=lerp(col,cc,cloudA);
    }
    col+=sunWide*0.45*cloudA;   // sun glow shining through cloud bodies

    return half4(max(col,0.0),1);
   }
   ENDHLSL
  }
 }
 FallBack Off
}
