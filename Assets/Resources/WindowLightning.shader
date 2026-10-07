Shader "HauntedFish/WindowLightning"
{
    Properties
    {
        _WindowMask ("Window silhouette", 2D) = "white" {}
        _LightColor ("Lightning color", Color) = (.72, .83, 1, 1)
        _Intensity ("Lightning intensity", Float) = 0
        [HideInInspector] _WindowBounds ("Window bounds", Vector) = (-4,-4,8,8)
        [HideInInspector] _TextureRect ("Texture rectangle", Vector) = (0,0,1,1)
        [HideInInspector] _Flip ("Sprite flip", Vector) = (0,0,0,0)
        [HideInInspector] _Ray ("Local light ray", Vector) = (0,-.45,1,0)
        [HideInInspector] _LightDirection ("World light ray", Vector) = (0,-.45,1,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "DisableBatching"="True" }
        Blend One One
        ColorMask RGB
        ZWrite Off
        ZTest LEqual
        Cull Back
        Offset -1, -1
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _WindowMask;
            float4x4 _WorldToWindow;
            float4 _WindowBounds, _TextureRect, _Flip, _LightColor, _Ray, _LightDirection;
            float _Intensity;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 vertex : SV_POSITION; float3 world : TEXCOORD0; float3 normal : TEXCOORD1; };
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }
            half4 frag(v2f i) : SV_Target
            {
                clip(abs(_Ray.z) - .0001);
                float3 local = mul(_WorldToWindow, float4(i.world, 1)).xyz;
                float distance = local.z / _Ray.z;
                clip(distance);
                float2 aperture = (local.xy - distance * _Ray.xy - _WindowBounds.xy) / _WindowBounds.zw;
                clip(min(min(aperture.x, aperture.y), min(1 - aperture.x, 1 - aperture.y)));
                float2 uv = lerp(aperture, 1 - aperture, _Flip.xy) * _TextureRect.zw + _TextureRect.xy;
                half4 mask = tex2D(_WindowMask, uv);
                // Black opaque artwork blocks light; transparent panes and white artwork transmit it.
                half transmission = 1 - mask.a * (1 - dot(mask.rgb, half3(.2126, .7152, .0722)));
                half facing = saturate(dot(normalize(i.normal), -_LightDirection.xyz));
                half attenuation = 1 / (1 + .012 * distance * distance);
                return half4(_LightColor.rgb * transmission * facing * attenuation * _Intensity, 0);
            }
            ENDCG
        }
    }
}
