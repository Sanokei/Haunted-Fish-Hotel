// Shared URP 17.3 thin-sheet lighting. No scene discovery or runtime material mutation.
#ifndef HAUNTED_ART_LIGHTING_INCLUDED
#define HAUNTED_ART_LIGHTING_INCLUDED
// Transparent receivers sample the shadow atlas, never the opaque screen-space depth.
#define _SURFACE_TYPE_TRANSPARENT 1
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
half3 ArtLightTerm(Light source, half3 normalWS)
{
    // Painted cards are two-sided thin surfaces, including camera-facing fish fins.
    return source.color * source.distanceAttenuation * source.shadowAttenuation * abs(dot(normalWS, source.direction));
}
half3 ArtIllumination(float3 positionWS, float4 positionCS, half3 normalWS,
    half ambient, half response, half lightLimit)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    normalWS = normalize(normalWS);
    // World-space lookup per fragment is important when one tall card crosses cascades.
    Light main = GetMainLight(TransformWorldToShadowCoord(positionWS), positionWS, half4(1,1,1,1));
    half3 direct = ArtLightTerm(main, normalWS);
#if USE_CLUSTER_LIGHT_LOOP
    UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        direct += ArtLightTerm(GetAdditionalLight(lightIndex, positionWS, half4(1,1,1,1)), normalWS);
    }
#endif
    uint lightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(lightCount)
        direct += ArtLightTerm(GetAdditionalLight(lightIndex, positionWS, half4(1,1,1,1)), normalWS);
    LIGHT_LOOP_END
    return ambient + min(direct * response, lightLimit.xxx);
}
half ArtHeightVisibility(float worldY, float startY, float endY, half minimum)
{
    return lerp(1.0h, minimum, smoothstep(startY, max(startY + .001, endY), worldY));
}
#endif
