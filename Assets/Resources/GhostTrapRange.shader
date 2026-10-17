Shader "HauntedFish/GhostTrapRange" {
Properties { _MainTex ("Fish soul", 2D) = "black" {} }
SubShader {
Tags { "Queue"="Transparent" "RenderType"="Transparent" }
Blend SrcAlpha OneMinusSrcAlpha
ZWrite Off
Cull Off
Pass {
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex; float4 _TextureRect, _Tiles; float _SharedTime;
struct input { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
struct output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
output vert(input v) { output o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }
half4 frag(output i):SV_Target {
// A school of small fish-soul ghosts in a tight brick pattern, tilted 45 degrees,
// all swimming together toward the top left with one shared bob.
float2 cell=i.uv*_Tiles.xy*2.6;
float2 swim=float2(.5,-.5)*_SharedTime;
cell+=swim+float2(.06,.06)*sin(_SharedTime*3.1);
float row=floor(cell.y);
cell.x+=.5*fmod(abs(row),2);
float2 local=frac(cell)-.5;
// Face the swim direction (top left): the fish art faces right, so turn it 135 degrees and mirror.
float angle=radians(135);
float s=sin(angle), c=cos(angle);
local=float2(c*local.x-s*local.y, s*local.x+c*local.y);
local.y=-local.y;
local=local*1.05+.5;
if(any(local<0)||any(local>1)) return half4(0,0,0,0);
float2 uv=lerp(_TextureRect.xy+.001,_TextureRect.xy+_TextureRect.zw-.001,local);
half4 col=tex2D(_MainTex,uv);
// Feather each ghost toward its cell edge instead of a hard alpha cut.
float2 edge=min(local,1-local);
col.a*=smoothstep(0,.22,min(edge.x,edge.y));
col.a*=.18; return col;
}
ENDCG
}
}
}