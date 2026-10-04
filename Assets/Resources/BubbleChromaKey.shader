Shader "UI/BubbleChromaKey"
{
    Properties
    {
        _MainTex ("Video", 2D) = "white" {}
        _Threshold ("Green threshold", Range(0, 1)) = 0.15
        _Softness ("Edge softness", Range(0.001, 0.5)) = 0.12
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            sampler2D _MainTex;
            float _Threshold, _Softness;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float green = c.g - max(c.r, c.b);
                c.a *= 1 - smoothstep(_Threshold, _Threshold + _Softness, green);
                c.g = min(c.g, max(c.r, c.b));
                return c * i.color;
            }
            ENDCG
        }
    }
}
