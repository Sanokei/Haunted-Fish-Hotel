Shader "HauntedFish/AtticReturnBlur"
{
    Properties {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BlurPixels ("Vertical smear in canvas pixels", Float) = 42
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="False" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            sampler2D _MainTex; fixed4 _Color; float _BlurPixels;
            v2f vert(appdata v) {
                v2f o;
                v.vertex.y += (v.uv.y * 2 - 1) * _BlurPixels;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = float2(v.uv.x, v.uv.y * 1.4 - .2);
                o.color = v.color * _Color; return o;
            }
            fixed4 frag(v2f i):SV_Target {
                fixed4 result = 0;
                [unroll] for (int k = -6; k <= 6; k++) {
                    float2 uv = i.uv + float2(0, k * .028);
                    fixed4 sample = tex2D(_MainTex, saturate(uv));
                    sample.a *= step(0, uv.y) * step(uv.y, 1);
                    result.rgb += sample.rgb * sample.a / 13;
                    result.a += sample.a / 13;
                }
                result.rgb /= max(result.a, .001);
                return result * i.color;
            }
            ENDCG
        }
    }
}
