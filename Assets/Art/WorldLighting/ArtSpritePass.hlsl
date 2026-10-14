#ifndef HAUNTED_ART_SPRITE_PASS_INCLUDED
#define HAUNTED_ART_SPRITE_PASS_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
#include "Assets/Art/WorldLighting/ArtLighting.hlsl"
TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
TEXTURE2D(_AlphaTex); SAMPLER(sampler_AlphaTex);
CBUFFER_START(UnityPerMaterial)
    half4 _Color;
    half _Ambient, _LightResponse, _LightLimit, _ShadowCutoff;
    float _DarknessStart, _DarknessEnd;
    half _DarknessFloor;
    float _EnableExternalAlpha;
#ifdef ART_ARM_FADE
    float _RootColorClearY, _RootColorPower, _RootLocalFeather;
#endif
CBUFFER_END
struct Attributes
{
    COMMON_2D_INPUTS
    half4 color : COLOR;
    UNITY_SKINNED_VERTEX_INPUTS
};
struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half fogFactor : TEXCOORD3;
    float rootLocalY : TEXCOORD4;
    half4 color : COLOR;
    UNITY_VERTEX_OUTPUT_STEREO
};
half4 ArtSpriteSample(float2 uv, half4 tint, float rootLocalY)
{
    half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
#ifdef ETC1_EXTERNAL_ALPHA
    texel.a = lerp(texel.a, SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv).r, _EnableExternalAlpha);
#endif
    texel *= tint;
#ifdef ART_ARM_FADE
    float t = saturate(rootLocalY / max(_RootColorClearY, .0001));
    texel.rgb *= pow(t * t * (3 - 2 * t), max(_RootColorPower, .01));
    texel.a *= smoothstep(0, max(_RootLocalFeather, .0001), rootLocalY);
#endif
    return texel;
}
Varyings ArtSpriteVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.rootLocalY = input.positionOS.y;
    UNITY_SKINNED_VERTEX_COMPUTE(input);
    SetUpSpriteInstanceProperties();
    input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
    output.positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    output.normalWS = TransformObjectToWorldNormal(float3(0,0,-1));
    output.uv = input.uv;
    output.color = input.color * _Color * unity_SpriteColor;
    output.fogFactor = ComputeFogFactor(output.positionCS.z);
    return output;
}
half4 ArtSpriteFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    half4 texel = ArtSpriteSample(input.uv, input.color, input.rootLocalY);
    texel.rgb *= ArtIllumination(input.positionWS, input.positionCS, input.normalWS, _Ambient, _LightResponse, _LightLimit);
    texel.rgb = MixFog(texel.rgb, input.fogFactor);
    texel.rgb *= ArtHeightVisibility(input.positionWS.y, _DarknessStart, _DarknessEnd, _DarknessFloor);
    return texel;
}
// Match sprite flip/skinning/tint/atlas UVs in BOTH passes. Never use an opaque quad UsePass.
float3 _LightDirection, _LightPosition;
Varyings ArtSpriteShadowVertex(Attributes input)
{
    Varyings output = ArtSpriteVertex(input);
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - output.positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    float3 normalWS = output.normalWS * (dot(output.normalWS, lightDirectionWS) < 0 ? -1 : 1);
    output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS, normalWS, lightDirectionWS));
    output.positionCS = ApplyShadowClamping(output.positionCS);
    return output;
}
half4 ArtSpriteShadowFragment(Varyings input) : SV_Target
{
    half alpha = ArtSpriteSample(input.uv, input.color, input.rootLocalY).a;
    clip(alpha - _ShadowCutoff);
    return 0;
}
#endif
