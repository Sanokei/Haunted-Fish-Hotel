// Opaque upper-wall finish: room architecture disappears above the playable room.
Shader "HauntedFish/LobbyHeightAtmosphere"
{
    Properties
    {
        _BaseColor ("Original plaster finish", Color) = (.35,.33,.28,1)
        _Ambient ("Indirect readability", Range(0,1)) = .32
        _LightResponse ("Direct response", Range(0,2)) = .85
        _LightLimit ("Direct limit", Range(0,3)) = 1.1
        _DarknessStart ("Room cornice height", Float) = 4.25
        _DarknessEnd ("Full atmospheric darkness height", Float) = 12
        _DarknessFloor ("Residual upper-room visibility", Range(0,1)) = .012
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite On
        Pass
        {
            Name "UpperRoomAtmosphere"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Assets/Art/WorldLighting/ArtLighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Ambient, _LightResponse, _LightLimit, _DarknessStart, _DarknessEnd, _DarknessFloor;
            CBUFFER_END
            struct Attributes { float3 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; half3 normal : TEXCOORD1; half fog : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world = TransformObjectToWorld(v.vertex);
                o.positionCS = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(v.normal);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 rgb = _BaseColor.rgb * ArtIllumination(i.world, i.positionCS, i.normal, _Ambient, _LightResponse, _LightLimit);
                rgb = MixFog(rgb, i.fog) * ArtHeightVisibility(i.world.y, _DarknessStart, _DarknessEnd, _DarknessFloor);
                return half4(rgb, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Ambient, _LightResponse, _LightLimit, _DarknessStart, _DarknessEnd, _DarknessFloor;
            CBUFFER_END
            float3 _LightDirection, _LightPosition;
            struct Attributes { float3 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 ShadowVertex(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 world = TransformObjectToWorld(input.vertex);
                float3 normalWS = TransformObjectToWorldNormal(input.normal);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - world);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                normalWS *= dot(normalWS, lightDirectionWS) < 0 ? -1 : 1;
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(world, normalWS, lightDirectionWS)));
            }
            half4 ShadowFragment() : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
