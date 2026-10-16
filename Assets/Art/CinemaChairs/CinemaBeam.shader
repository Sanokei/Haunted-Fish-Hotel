Shader "HauntedFish/CinemaBeam" {SubShader{Tags{"Queue"="Transparent" "RenderType"="Transparent"}Blend SrcAlpha One ZWrite Off Cull Off Pass{CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct a{float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};struct v{float4 position:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
float hash11(float p){p=frac(p*.1031);p*=p+33.33;p*=p+p;return frac(p);}
float hash21(float2 p){float3 q=frac(float3(p.xyx)*.1031);q+=dot(q,q.yzx+33.33);return frac((q.x+q.y)*q.z);}
float noise21(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),lerp(hash21(i+float2(0,1)),hash21(i+1),f.x),f.y);}
v vert(a i){v o;o.position=UnityObjectToClipPos(i.vertex);o.color=i.color;o.uv=i.uv;return o;}
fixed4 frag(v i):SV_Target{
 float edge=sin(saturate(i.uv.x)*3.14159);
 // Same 16 fps shutter as the film so the beam pulses with each frame.
 float frame=floor(_Time.y*16);float shutter=.88+.24*hash11(frame*.618);
 // Smoke haze drifting through the throw, and a scatter of lit dust motes.
 float haze=.75+.5*noise21(i.uv*float2(6,3)+float2(_Time.y*.08,_Time.y*.03));
 float2 m=i.uv*float2(60,30)+float2(_Time.y*.6,-_Time.y*.25);float mote=step(.985,hash21(floor(m)))*(1-smoothstep(.1,.35,length(frac(m)-.5)));
 return fixed4(.62,.6,.5,i.color.a*edge*(shutter*haze+mote*4));}
ENDCG}}}
