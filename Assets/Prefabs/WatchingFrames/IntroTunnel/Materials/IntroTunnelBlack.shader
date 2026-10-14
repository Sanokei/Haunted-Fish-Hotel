Shader "HauntedFish/IntroTunnelBlack" {
 SubShader { Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
 Pass { Tags { "LightMode"="SRPDefaultUnlit" } Cull Back ZWrite On ZTest LEqual
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct A { float3 vertex : POSITION; }; struct V { float4 vertex : SV_POSITION; };
 V vert(A i) { V o; o.vertex=TransformObjectToHClip(i.vertex); return o; }
 half4 frag(V i) : SV_Target { return half4(0,0,0,1); }
 ENDHLSL
 } } FallBack Off
}
