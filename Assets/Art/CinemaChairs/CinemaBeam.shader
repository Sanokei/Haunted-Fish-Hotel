Shader "HauntedFish/CinemaBeam" {SubShader{Tags{"Queue"="Transparent" "RenderType"="Transparent"}Blend SrcAlpha One ZWrite Off Cull Off Pass{CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct a{float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};struct v{float4 position:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};v vert(a i){v o;o.position=UnityObjectToClipPos(i.vertex);o.color=i.color;o.uv=i.uv;return o;}fixed4 frag(v i):SV_Target{float edge=sin(saturate(i.uv.x)*3.14159);return fixed4(.45,.62,.52,i.color.a*edge*(.96+.04*sin(_Time.y*2.513)));}
ENDCG}}}
