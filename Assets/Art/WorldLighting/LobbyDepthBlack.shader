// Opaque authored lining behind the staircase. Fog/sky/light cannot expose the outside void.
Shader "HauntedFish/LobbyDepthBlack"
{
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite On
        Pass
        {
            Name "HiddenRoomDepth"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float3 vertex : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.vertex); return o; }
            half4 Frag(Varyings i) : SV_Target { return half4(0,0,0,1); }
            ENDHLSL
        }
    }
    FallBack Off
}
