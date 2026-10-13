Shader "HauntedFish/CinemaFilm"
{
 Properties{_MainTex("Decoded film",2D)="black"{} _Grain("Grain",Range(0,.02))=.008 _Vignette("Vignette",Range(0,.08))=.035 _Flicker("Slow exposure drift",Range(0,.015))=.0075}
 SubShader{Tags{"RenderType"="Opaque" "Queue"="Geometry+20"}Cull Off ZWrite On
 Pass{CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex;float4 _MainTex_ST;float _Grain,_Vignette,_Flicker;
 struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;};struct v2f{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
 v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.uv,_MainTex);return o;}
 fixed4 frag(v2f i):SV_Target
 {fixed4 c=tex2D(_MainTex,i.uv);float noise=frac(sin(dot(floor(i.uv*float2(640,360))+floor(_Time.y*2),float2(12.9898,78.233)))*43758.5453)-.5;float edge=smoothstep(.20,.72,length(i.uv-.5));c.rgb=saturate(c.rgb*(1-_Vignette*edge)*(1+_Flicker*sin(_Time.y*2.513))+noise*_Grain);c.a=1;return c;}
 ENDCG}}
}
