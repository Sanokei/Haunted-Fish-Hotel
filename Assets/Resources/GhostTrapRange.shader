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
float2 drift=float2(.04,-.06)*_SharedTime + float2(.03*sin(_SharedTime*1.4),0);
float2 uv=frac(i.uv*_Tiles.xy+drift);
uv=lerp(_TextureRect.xy+.001,_TextureRect.xy+_TextureRect.zw-.001,uv);
half4 c=tex2D(_MainTex,uv); c.a*=.22; return c;
}
ENDCG
}
}
}