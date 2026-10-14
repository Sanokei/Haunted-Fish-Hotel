// Surface-only extension of URP's installed Lit implementation. Lighting, weave,
// shadows and fog remain URP Lit; no generated texture or runtime material is used.
Shader "HauntedFishHotel/Lobby/CarpetDamask"
{
    Properties
    {
        [MainTexture] _BaseMap("Carpet albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Burgundy felt", Color) = (.48,.16,.19,1)
        [NoScaleOffset] _DamaskMap("Original CC0 wallpaper", 2D) = "white" {}
        _DamaskGold("Muted gold pigment", Color) = (.66,.49,.26,1)
        _DamaskStrength("Faint pattern opacity", Range(0,1)) = .24
        _DamaskMaskLow("Linear dark motif edge", Range(0,1)) = .04
        _DamaskMaskHigh("Linear pale ground edge", Range(0,1)) = .36
        _DamaskUV("Existing UV scale XY, offset ZW", Vector) = (.02551020408,.03433476395,.5,.25751072961)
        [Normal] _BumpMap("Existing carpet weave", 2D) = "bump" {}
        _BumpScale("Weave strength", Float) = .22
        _Smoothness("Matte roughness complement", Range(0,1)) = .06
        _Metallic("Metallic", Range(0,1)) = 0
        _SpecColor("Specular", Color) = (.2,.2,.2,1)
        _MetallicGlossMap("Metallic", 2D) = "white" {}
        _SpecGlossMap("Specular", 2D) = "white" {}
        _OcclusionMap("Occlusion", 2D) = "white" {}
        _OcclusionStrength("Occlusion strength", Float) = 1
        _EmissionMap("Emission", 2D) = "white" {}
        _EmissionColor("Emission", Color) = (0,0,0,1)
        _DetailMask("Detail mask", 2D) = "white" {}
        _DetailAlbedoMap("Detail", 2D) = "linearGrey" {}
        _DetailAlbedoMapScale("Detail strength", Float) = 1
        _DetailNormalMap("Detail normal", 2D) = "bump" {}
        _DetailNormalMapScale("Detail normal strength", Float) = 1
        [HideInInspector] _Cutoff("Cutoff", Float) = .5
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _ClearCoatMask("Clear coat", Float) = 0
        [HideInInspector] _ClearCoatSmoothness("Clear coat smoothness", Float) = 0
        [HideInInspector] _Parallax("Parallax", Float) = .005
        [HideInInspector] _ReceiveShadows("Receive shadows", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" }
        LOD 300
        HLSLINCLUDE
        #define InitializeStandardLitSurfaceData InitializeCarpetBaseSurfaceData
        #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
        #undef InitializeStandardLitSurfaceData
        TEXTURE2D(_DamaskMap); SAMPLER(sampler_DamaskMap);
        // The sole carpet material has a separate extension buffer. It deliberately
        // forgoes SRP batching rather than modifying Unity's versioned Lit buffer.
        CBUFFER_START(CarpetPattern)
        float4 _DamaskUV;
        half4 _DamaskGold;
        half _DamaskStrength, _DamaskMaskLow, _DamaskMaskHigh;
        CBUFFER_END
        void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
        {
            InitializeCarpetBaseSurfaceData(uv, surface);
            // The authored mesh has UV = local XZ * 2.5 for its weave. Map the
            // unaltered wallpaper once over X[-7.84,7.84], Z[-3,8.65].
            float2 patternUV = saturate(uv * _DamaskUV.xy + _DamaskUV.zw);
            half3 photograph = SAMPLE_TEXTURE2D(_DamaskMap, sampler_DamaskMap, patternUV).rgb;
            half luminance = dot(photograph, half3(.2126,.7152,.0722));
            half motif = 1.0h - smoothstep(_DamaskMaskLow, _DamaskMaskHigh, luminance);
            surface.albedo = lerp(surface.albedo, _DamaskGold.rgb, saturate(motif * _DamaskStrength));
            // This is dyed yarn, not metallic foil, emissive ink or a clear coat.
            surface.metallic = 0;
            surface.smoothness = _Smoothness;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit
            #pragma shader_feature EDITOR_VISUALIZATION
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
