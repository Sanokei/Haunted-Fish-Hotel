Shader "HauntedFish/IntroTunnelHeightFade" {
 Properties { _FadeStart ("World height where darkness begins", Float)=25 _FadeRate ("Height extinction", Float)=0.1 }
 SubShader { Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
 Pass { Tags { "LightMode"="SRPDefaultUnlit" } Cull Back ZWrite Off ZTest LEqual Blend One OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial) float _FadeStart, _FadeRate; CBUFFER_END
 struct A { float3 vertex : POSITION; }; struct V { float4 vertex : SV_POSITION; float height:TEXCOORD0; };
 V vert(A i) { V o; float3 p=TransformObjectToWorld(i.vertex); o.vertex=TransformWorldToHClip(p); o.height=p.y; return o; }
 half4 frag(V i) : SV_Target { return half4(0,0,0,1-exp(-max(0,i.height-_FadeStart)*_FadeRate)); }
 ENDHLSL
 } } FallBack Off
}
