// One draw owns original frame, opaque grey opening and its clipped watching eye.
// Deliberately no stencil state, SpriteMask, screen grab, UI mask or global material writes.
Shader "HauntedFish/WatchingEye"
{
    Properties
    {
        _MainTex ("Original filled or unfilled frame", 2D) = "black" {}
        _HasEye ("Authored watching-eye slot enabled", Range(0,1)) = 1
        _PupilIntensity ("Authored pupil display balance", Range(0,1)) = 1
        _HasPicture ("Future picture slot enabled", Range(0,1)) = 0
        _PictureTex ("Future original picture inside this frame", 2D) = "black" {}
        _PictureRect ("Picture center XY and size ZW in frame UV", Vector) = (.5,.5,1,1)
        _FrameOpening ("This original frame's isolated opening", 2D) = "black" {}
        _FrameBacking ("Opaque backing beneath original inner paint", 2D) = "black" {}
        _OutlineTex ("Original eye outline", 2D) = "black" {}
        _PupilTex ("Original red pupil", 2D) = "black" {}
        _InteriorMask ("Interior of original eye outline", 2D) = "black" {}
        _CullMode ("Authored face culling (0 double-sided, 2 inward-only)", Float) = 0
        _InteriorColor ("Opaque interior (linear RGB)", Vector) = (0.014443843596,0.014443843596,0.014443843596,1)
        _EyeRect ("Height-first eye center XY and size ZW in frame UV", Vector) = (.5,.5,1,1)
        _Ambient ("Indirect art readability", Range(0,1)) = .4
        _LightResponse ("Non-pupil 3D light response", Range(0,2)) = .85
        _LightLimit ("Maximum direct illumination", Range(0,3)) = 1.1
        _ShadowCutoff ("Original silhouette shadow contour", Range(.01,1)) = .2
        _DarknessStart ("Atmospheric darkness start world Y", Float) = 10000
        _DarknessEnd ("Atmospheric darkness end world Y", Float) = 10001
        _DarknessFloor ("Upper darkness residual", Range(0,1)) = 1
        _NearArtVisibility ("Faint nearby frame visibility", Range(0,1)) = .1
        _NearArtDistance ("Nearby visibility falloff distance", Float) = 9
        [PerRendererData] _PresentationGain ("Authored cinematic depth fade", Range(0,1)) = 1
        [PerRendererData] _Closure ("Eyelids closed", Range(0,1)) = 0
        _ClosedLineWidth ("Closed lid half thickness", Float) = .006
        [PerRendererData] _PupilOffset ("Pupil travel in eye UV", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend One OneMinusSrcAlpha
        Cull [_CullMode]
        ZWrite Off
        ZTest LEqual
        Pass
        {
            Name "IsolatedFrameComposite"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Art/WorldLighting/ArtLighting.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_FrameOpening); SAMPLER(sampler_FrameOpening);
            TEXTURE2D(_FrameBacking); SAMPLER(sampler_FrameBacking);
            TEXTURE2D(_OutlineTex); SAMPLER(sampler_OutlineTex);
            TEXTURE2D(_PupilTex); SAMPLER(sampler_PupilTex);
            TEXTURE2D(_PictureTex); SAMPLER(sampler_PictureTex);
            TEXTURE2D(_InteriorMask); SAMPLER(sampler_InteriorMask);
            CBUFFER_START(UnityPerMaterial)
                float4 _EyeRect, _PupilOffset, _InteriorColor, _PictureRect;
                float _Closure, _ClosedLineWidth, _Ambient, _LightResponse, _LightLimit, _PresentationGain;
                float _ShadowCutoff, _DarknessStart, _DarknessEnd, _DarknessFloor;
                float _HasEye, _HasPicture, _NearArtVisibility, _NearArtDistance, _PupilIntensity;
            CBUFFER_END
            struct appdata { float3 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 world : TEXCOORD1;
                half3 normal : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            v2f vert(appdata v)
            {
                v2f o = (v2f)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world = TransformObjectToWorld(v.vertex);
                o.vertex = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(float3(0,0,-1));
                o.uv = v.uv; o.fogFactor = ComputeFogFactor(o.vertex.z); return o;
            }
            float inside(float2 uv)
            {
                return step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
            }
            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 frame = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half opening = SAMPLE_TEXTURE2D(_FrameOpening, sampler_FrameOpening, i.uv).a;
                half backing = SAMPLE_TEXTURE2D(_FrameBacking, sampler_FrameBacking, i.uv).a;
                float2 eyeUV = (i.uv - _EyeRect.xy) / max(_EyeRect.zw, .0001) + .5;
                float eyeBounds = inside(eyeUV);
                float2 pupilUV = eyeUV - _PupilOffset.xy;
                half4 pupil = SAMPLE_TEXTURE2D(_PupilTex, sampler_PupilTex, pupilUV);
                float closure = saturate(_Closure);
                float aperture = max(.001, 1 - closure);
                float2 lidUV = float2(eyeUV.x, (eyeUV.y - .5) / aperture + .5);
                float lidBounds = inside(lidUV);
                // Height stays fixed; only the lids converge. Pupil shape never stretches.
                pupil.a *= SAMPLE_TEXTURE2D(_InteriorMask, sampler_InteriorMask, lidUV).a * inside(pupilUV) * lidBounds;
                half4 outline = SAMPLE_TEXTURE2D(_OutlineTex, sampler_OutlineTex, lidUV);
                outline.a *= lidBounds;
                float closedBlend = smoothstep(.92, 1, closure);
                float closedLine = (1 - smoothstep(_ClosedLineWidth,
                    _ClosedLineWidth + max(.0005, fwidth(eyeUV.y)), abs(eyeUV.y - .5))) *
                    smoothstep(.006, .018, eyeUV.x) * smoothstep(.006, .018, 1 - eyeUV.x);
                half3 lineColor = SAMPLE_TEXTURE2D(_OutlineTex, sampler_OutlineTex, float2(.5, .985)).rgb;
                outline.rgb = lerp(outline.rgb, lineColor, closedBlend);
                outline.a = lerp(outline.a, closedLine, closedBlend);
                pupil.a *= (1 - closedBlend) * saturate(_HasEye);
                outline.a *= saturate(_HasEye);
                // Separate pupil energy before lighting. Its RGB never samples shadow attenuation.
                half pupilAlpha = pupil.a * (1 - outline.a) * eyeBounds;
                half3 pupilRGB = pupil.rgb * pupilAlpha * saturate(_PupilIntensity);
                half outlineAlpha = outline.a * eyeBounds;
                half eyeAlpha = outlineAlpha + pupilAlpha;
                float2 pictureUV = (i.uv - _PictureRect.xy) / max(_PictureRect.zw, .0001) + .5;
                half4 picture = SAMPLE_TEXTURE2D(_PictureTex, sampler_PictureTex, pictureUV);
                half3 backingRGB = lerp(_InteriorColor.rgb, picture.rgb, picture.a * inside(pictureUV) * saturate(_HasPicture));
                half3 pictureSurface = outline.rgb * outlineAlpha + backingRGB * (1 - eyeAlpha);
                // The FRAME mask is applied after all eye/pupil/closed-line composition.
                // It clips over-wide height-fitted eyes independently for every frame.
                half alpha = frame.a + backing * (1 - frame.a);
                half3 surfaceRGB = frame.rgb * frame.a * (1 - opening) + pictureSurface * opening +
                    _InteriorColor.rgb * backing * (1 - frame.a) * (1 - opening);
                half3 illumination = ArtIllumination(i.world, i.vertex, i.normal, _Ambient, _LightResponse, _LightLimit);
                // Pupils bypass direct/indirect light and shadow response only. The entire picture
                // still belongs in the same atmospheric room and must disappear into distant fog.
                half3 litSurface = surfaceRGB * illumination;
                half3 rgb = litSurface + pupilRGB * opening;
                rgb = MixFog(rgb / max(alpha, .0001), i.fogFactor) * alpha;
                half heightVisibility = ArtHeightVisibility(i.world.y, _DarknessStart, _DarknessEnd, _DarknessFloor);
                float viewerDistance = distance(_WorldSpaceCameraPos, i.world);
                half nearVisibility = _NearArtVisibility * (1 - smoothstep(_NearArtDistance * .3, max(.001, _NearArtDistance), viewerDistance));
                // A faint nearby paint value reveals original frame contours without revealing the far walls.
                rgb *= max(heightVisibility, nearVisibility);
                // Backing is opaque; exterior alpha is original paint. Pupils remain light/shadow independent.
                clip(alpha - .001);
                return half4(rgb * saturate(_PresentationGain), alpha);
            }
            ENDHLSL
        }
        // One color composite remains; this depth-only pass has no stencil or pupil texture.
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
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_FrameBacking); SAMPLER(sampler_FrameBacking);
            CBUFFER_START(UnityPerMaterial)
                float4 _EyeRect, _PupilOffset, _InteriorColor, _PictureRect;
                float _Closure, _ClosedLineWidth, _Ambient, _LightResponse, _LightLimit, _PresentationGain;
                float _ShadowCutoff, _DarknessStart, _DarknessEnd, _DarknessFloor;
                float _HasEye, _HasPicture, _NearArtVisibility, _NearArtDistance, _PupilIntensity;
            CBUFFER_END
            float3 _LightDirection, _LightPosition;
            struct Attributes { float3 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings ShadowVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output = (Varyings)0;
                float3 world = TransformObjectToWorld(input.vertex);
                float3 normalWS = TransformObjectToWorldNormal(float3(0,0,-1));
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - world);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                normalWS *= dot(normalWS, lightDirectionWS) < 0 ? -1 : 1;
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(world, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                output.uv = input.uv;
                return output;
            }
            half4 ShadowFragment(Varyings input) : SV_Target
            {
                half paint = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                half backing = SAMPLE_TEXTURE2D(_FrameBacking, sampler_FrameBacking, input.uv).a;
                // Opaque original opening/backing belongs to the frame. Pupil movement cannot change its shadow.
                clip(paint + backing * (1 - paint) - _ShadowCutoff);
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
