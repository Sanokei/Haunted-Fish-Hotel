Shader "HauntedFish/GhostPlacementGrid"
{
    Properties
    {
        _GridColor ("Grid Color", Color) = (0.3, 0.7, 1.0, 0.2)
        _SDFRadius ("SDF Radius", Float) = 5.0
        _SDFFeather ("SDF Feather", Float) = 1.2
        _NoiseScale ("Noise Scale", Float) = 2.2
        _NoiseAmplitude ("Noise Amplitude", Float) = 0.4
        _NoiseSpeed ("Noise Speed", Float) = 4.0
        _ToonSteps ("Toon Steps", Float) = 3.0

        // Wobbly line settings for hand-drawn look
        _WobbleScale ("Wobble Scale", Float) = 15.0
        _WobbleAmplitude ("Wobble Amplitude", Float) = 0.3
        _WobbleSpeed ("Wobble Speed", Float) = 6.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        LOD 100

        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float3 localPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            float _SDFRadius;
            float _SDFFeather;
            float _NoiseScale;
            float _NoiseAmplitude;
            float _NoiseSpeed;
            float _ToonSteps;

            float _WobbleScale;
            float _WobbleAmplitude;
            float _WobbleSpeed;

            // Pseudo-random 3D noise
            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + float3(0.1, 0.1, 0.1));
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                
                return lerp(
                    lerp(lerp(hash(i + float3(0,0,0)), hash(i + float3(1,0,0)), f.x),
                         lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x), f.y),
                    lerp(lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x),
                         lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x), f.y), f.z
                );
            }

            // Box SDF
            float sdBox(float3 p, float3 b)
            {
                float3 q = abs(p) - b;
                return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.localPos = v.vertex.xyz;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. Hand-drawn wobbly edge line styling
                // i.uv.x goes from 0 to 1 across the width of the line.
                // Compute distance from center of line (range [0, 1]).
                float distToEdge = abs(i.uv.x - 0.5) * 2.0;

                // Sample high-frequency animated noise for wobble
                float3 wobbleCoord = i.localPos * _WobbleScale + float3(0.0, _Time.y * _WobbleSpeed, 0.0);
                float w = noise(wobbleCoord) * _WobbleAmplitude;
                float wobblyEdge = distToEdge + w;

                // Discard pixels near the line edge to make it look sketchy and hand-drawn
                if (wobblyEdge > 1.0)
                {
                    discard;
                }

                // 2. SDF Boundary Masking (waved sides)
                // Calculate base SDF around the object center (using Box SDF)
                float3 boxHalfSize = float3(_SDFRadius, _SDFRadius, _SDFRadius) * 0.7;
                float baseDist = sdBox(i.localPos, boxHalfSize);

                // Sample animated noise for SDF wave edge
                float3 noiseCoord = i.localPos * _NoiseScale + float3(0.0, _Time.y * _NoiseSpeed, _Time.z * _NoiseSpeed * 0.5);
                float n = noise(noiseCoord) * 2.0 - 1.0; // range [-1, 1]

                // Perturb the distance field with noise to make it organic/wavy/random
                float perturbedDist = baseDist + n * _NoiseAmplitude;

                // Feathering factor
                float t = saturate(1.0 - (perturbedDist / max(0.01, _SDFFeather)));

                // Toon step effect
                if (_ToonSteps > 1.0)
                {
                    t = floor(t * _ToonSteps) / (_ToonSteps - 1.0);
                }

                // Final alpha includes vertex alpha (for color coding axes) and toon mask
                float alpha = i.color.a * saturate(t);

                // Add a subtle scanline or sketch pattern for the toon look
                float sketchVal = sin(i.localPos.x * 40.0) * sin(i.localPos.z * 40.0);
                float sketchMask = step(0.0, sketchVal);
                alpha *= lerp(0.75, 1.0, sketchMask);

                // Early exit/discard if invisible
                if (alpha <= 0.005)
                {
                    discard;
                }

                return fixed4(i.color.rgb, alpha);
            }
            ENDCG
        }
    }
}

