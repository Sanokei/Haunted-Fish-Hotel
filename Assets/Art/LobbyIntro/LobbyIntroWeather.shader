Shader "HauntedFish/UI/Lobby Intro Weather"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [Enum(Sky,0,Rain,1,WetGround,2,LampGlow,3,Paper,4,Envelope,5)] _Mode ("Layer", Float) = 0
        _Density ("Rain cells across", Float) = 38
        _Speed ("Fall speed", Float) = 1.1
        _Wind ("Wind slant", Float) = 0.14
        _Seed ("Layer seed", Float) = 0
        _Opacity ("Layer opacity", Range(0,1)) = 0.3
        _Length ("Drop length", Range(0.05,0.8)) = 0.32
        _Width ("Drop width", Range(0.004,0.15)) = 0.035
        _Aspect ("Authored width / height", Float) = 1.7777778
        _Light ("Light visibility", Range(0,1)) = 1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            Name "AuthoredIntroWeather"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 localPosition : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            fixed4 _Color;
            float4 _ClipRect;
            float _Mode, _Density, _Speed, _Wind, _Seed, _Opacity, _Length, _Width, _Aspect, _Light;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.localPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f*f*(3.0-2.0*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            float cloudNoise(float2 p)
            {
                return noise(p)*0.56 + noise(p*2.03+5.7)*0.28 + noise(p*4.11+13.1)*0.16;
            }
            float lampVisibility(float2 uv)
            {
                // Gate lamps and the cool open patch in the sky selectively catch the rain.
                float a = exp(-dot((uv-float2(0.15,0.58))*float2(6,4),(uv-float2(0.15,0.58))*float2(6,4)));
                float b = exp(-dot((uv-float2(0.85,0.58))*float2(6,4),(uv-float2(0.85,0.58))*float2(6,4)));
                float sky = exp(-dot((uv-float2(0.56,0.78))*float2(2.5,4),(uv-float2(0.56,0.78))*float2(2.5,4)));
                return saturate(0.10 + (a+b)*0.70*_Light + sky*0.42);
            }
            float rain(float2 uv, float time)
            {
                // A staggered grid of individual soft droplets. There are no line primitives.
                float2 p = float2(uv.x*_Aspect + uv.y*_Wind, uv.y) * _Density;
                float column = floor(p.x);
                p.y += time*_Speed*_Density*(0.77 + hash(float2(column,_Seed))*0.46);
                p.y += hash(float2(column,9.2+_Seed))*17.0;
                float2 cell = floor(p);
                float n = hash(cell+_Seed), n2 = hash(cell.yx+_Seed+19.3);
                float2 q = frac(p)-float2(0.2+n*0.6,0.5);
                float length = _Length*lerp(0.6,1.45,n2);
                float along = q.y/length;
                // Streak is brightest at its trailing end and fades toward the leading edge, like motion-blurred drops.
                float taper = saturate(1.0-abs(along)*2.0)*lerp(1.0,0.45,saturate(along*0.5+0.5));
                float width = _Width*lerp(0.6,1.5,n)*sqrt(taper+0.015);
                float aa = max(fwidth(p.x)*0.7,0.004);
                float crossSection = 1.0-smoothstep(width*0.15,width+aa,abs(q.x));
                float body = crossSection*pow(taper,0.85);
                // Broken specular response and a tiny softer head avoid rigid white strokes.
                float breakup = lerp(0.35,1.0,noise(float2(q.y*39.0,n*71.0)));
                float head = 0.0;
                return saturate(body*breakup+head)*lerp(0.25,0.95,n2)*step(0.19,n);
            }
            float ripple(float2 uv, float time)
            {
                float2 p = uv*float2(15.0,5.0);
                float2 cell = floor(p);
                float n = hash(cell+_Seed);
                float phase = frac(time*0.47+n*7.0);
                float2 q = frac(p)-float2(0.3+n*0.4,0.28+hash(cell+31.0)*0.44);
                float radius = phase*0.54;
                float ring = 1.0-smoothstep(0.012,0.045,abs(length(q*float2(1,2.6))-radius));
                return ring*pow(1.0-phase,3.0)*step(0.67,n)*smoothstep(0.05,0.4,uv.y);
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float time = _Time.y;
                float4 c = 0;
                if (_Mode < 0.5)
                {
                    float clouds = cloudNoise(uv*float2(4.3,3.0)+float2(time*0.012,0));
                    float horizon = pow(1.0-uv.y,1.7);
                    float3 sky = lerp(float3(0.018,0.027,0.051),float3(0.075,0.10,0.14),horizon);
                    sky += float3(0.025,0.035,0.049)*smoothstep(0.34,0.85,clouds);
                    sky *= 0.72+0.28*(1.0-smoothstep(0.25,0.75,abs(uv.x-0.5)));
                    c = float4(sky,1);
                }
                else if (_Mode < 1.5)
                {
                    float visibility = lampVisibility(uv);
                    float amount = rain(uv,time)*visibility*_Opacity;
                    // Blue-grey scattering with dim warm highlights beside the lamps.
                    float warm = saturate((1.0-abs(uv.y-0.58)*4.0)*(1.0-abs(abs(uv.x-0.5)-0.35)*7.0))*_Light;
                    c = float4(lerp(float3(0.46,0.61,0.72),float3(0.74,0.67,0.48),warm*0.55),amount);
                }
                else if (_Mode < 2.5)
                {
                    float path = 1.0-smoothstep(lerp(0.44,0.09,uv.y),lerp(0.49,0.14,uv.y),abs(uv.x-0.5));
                    float grain = cloudNoise(uv*float2(11,27));
                    float3 ground = lerp(float3(0.018,0.026,0.031),float3(0.068,0.077,0.089),path*(0.58+grain*0.42));
                    float wet = noise(uv*float2(13,53))*noise(uv*float2(4,17));
                    ground += float3(0.040,0.052,0.062)*wet*path;
                    ground += float3(0.22,0.25,0.26)*ripple(uv,time)*path*_Opacity;
                    c = float4(ground,1.0-smoothstep(0.82,1.0,uv.y));
                }
                else if (_Mode < 3.5)
                {
                    float2 q = (uv-0.5)*2.0;
                    c = float4(1.0,0.73,0.33,exp(-dot(q,q)*4.8)*_Opacity);
                }
                else
                {
                    // Authored blank stationery; narrative text remains a separate Ink/TMP concern.
                    float grain = noise(uv*float2(260,160))*0.026;
                    float edge = smoothstep(0.0,0.04,min(min(uv.x,1-uv.x),min(uv.y,1-uv.y)));
                    float3 paper = float3(0.65,0.61,0.48)+grain+edge*0.11;
                    if (_Mode > 4.5)
                    {
                        float crease = abs(uv.y-(1.0-abs(uv.x-0.5)*1.25));
                        paper -= (1.0-smoothstep(0.002,0.014,crease))*0.18;
                    }
                    c = float4(paper,1);
                }
                c *= i.color;
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.localPosition.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a-0.001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
