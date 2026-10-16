// Placement preview in the style of Mix and Jam's Luigi's Mansion ghost (ghost.shadergraph): an additive HDR-blue
// Fresnel rim. A faint body fill and a slow shimmer keep flat-faced defenses readable from the table camera.
Shader "HauntedFish/GhostPlacement"
{
    Properties
    {
        [HDR] _Color ("Ghost colour", Color) = (0, 1.0097, 2.4151, 1)
        _FresnelPower ("Fresnel power", Range(.25, 8)) = 1
        _Body ("Body fill", Range(0, 1)) = .18
        _Shimmer ("Shimmer strength", Range(0, 1)) = .25
        _ShimmerSpeed ("Shimmer speed", Float) = 2.2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Back
        Pass
        {
            Name "GhostForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _FresnelPower, _Body, _Shimmer, _ShimmerSpeed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; half fog : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS; o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 view = normalize(GetWorldSpaceViewDir(i.positionWS));
                half fresnel = pow(1 - saturate(dot(n, view)), _FresnelPower);
                half shimmer = 1 - _Shimmer * (.5 + .5 * sin(_Time.y * _ShimmerSpeed + i.positionWS.y * 6));
                half3 c = _Color.rgb * (fresnel + _Body) * shimmer * _Color.a;
                return half4(MixFogColor(c, half3(0, 0, 0), i.fog), 1);
            }
            ENDHLSL
        }
    }
}
