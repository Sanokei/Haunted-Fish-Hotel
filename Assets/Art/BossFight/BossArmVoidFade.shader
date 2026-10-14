// Authored two-sided 3D-lit artwork; original-alpha shadow caster shares all sprite transforms.
Shader "HauntedFish/BossArmVoidFade"
{
    Properties
    {
        [PerRendererData] _MainTex ("Original sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Ambient ("Indirect art readability", Range(0,1)) = .48
        _LightResponse ("3D light and shadow response", Range(0,2)) = .85
        _LightLimit ("Maximum direct illumination", Range(0,3)) = 1.1
        _ShadowCutoff ("Original-alpha shadow contour", Range(.01,1)) = .2
        _DarknessStart ("Atmospheric darkness start world Y", Float) = 10000
        _DarknessEnd ("Atmospheric darkness end world Y", Float) = 10001
        _DarknessFloor ("Upper darkness residual", Range(0,1)) = 1
        [HideInInspector] _RendererColor ("Renderer tint", Color) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("External alpha enabled", Float) = 0
        _RootColorClearY ("Sprite Local Y: Original Color", Float) = 1.6
        _RootColorPower ("Root Darkness Power", Range(.1,4)) = 1.35
        _RootLocalFeather ("Sprite Local Y Alpha Feather", Float) = .8
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" "DisableBatching"="True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual
        Pass
        {
            Name "ArtForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #pragma vertex ArtSpriteVertex
            #pragma fragment ArtSpriteFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #define ART_ARM_FADE 1
            #include "Assets/Art/WorldLighting/ArtSpritePass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Blend Off
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #pragma vertex ArtSpriteShadowVertex
            #pragma fragment ArtSpriteShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #define ART_ARM_FADE 1
            #include "Assets/Art/WorldLighting/ArtSpritePass.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
