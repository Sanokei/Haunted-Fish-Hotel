Shader "HauntedFish/GameMouseSpotlight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Mouse ("Mouse", Vector) = (.5,.5,0,0)
        _Aspect ("Aspect", Float) = 1
        _Radius ("Radius", Float) = .18
        _Softness ("Soft edge", Float) = .12
        _Darkness ("Darkness", Range(0,1)) = .92
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
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            float4 _Mouse;
            float _Aspect, _Radius, _Softness, _Darkness;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            half4 frag(v2f i) : SV_Target
            {
                float2 offset = i.uv - _Mouse.xy;
                offset.x *= _Aspect;
                float darkness = smoothstep(_Radius, _Radius + max(.001, _Softness), length(offset));
                return half4(0, 0, 0, darkness * _Darkness);
            }
            ENDCG
        }
    }
}
