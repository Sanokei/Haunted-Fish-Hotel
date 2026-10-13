Shader "HauntedFish/AtticReturnBlur"
{
    Properties {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BlurPixels ("Directional smear in screen pixels", Float) = 42
        _Direction ("Smear axis (X horizontal, Y vertical)", Vector) = (0,1,0,0)
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
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            sampler2D _MainTex; fixed4 _Color; float _BlurPixels; float4 _Direction;
            v2f vert(appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color; return o;
            }
            fixed4 frag(v2f i):SV_Target {
                fixed4 result = 0;
                // Screen derivatives keep the smear horizontal/vertical even on a
                // tilted fish, with the same width on large panels and small props.
                // Geometry/UV scale stays unchanged, so adjacent panels do not shrink.
                float2 smear = (ddx(i.uv) * _Direction.x + ddy(i.uv) * _Direction.y) * _BlurPixels;
                [unroll] for (int k = -6; k <= 6; k++) {
                    float2 uv = i.uv + smear * (k / 6.0);
                    fixed4 sample = tex2D(_MainTex, saturate(uv));
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
