Shader "HauntedFish/CinemaFilm"
{
 // Projected-film look on the screen surface only: the room itself stays clean.
 // Everything steps at the projector's frame rate so weave, flicker, dust and scratches read as film, not as digital noise.
 Properties
 {
  _MainTex("Decoded film",2D)="black"{}
  _Fps("Projector frames per second",Range(8,24))=16
  _Sepia("Sepia tone",Range(0,1))=.85
  _Grain("Grain",Range(0,.25))=.06
  _Flicker("Exposure flicker",Range(0,.3))=.12
  _Weave("Gate weave",Range(0,.01))=.0025
  _Jolt("Frame jolt chance",Range(0,.2))=.04
  _Dust("Dust and hair",Range(0,1))=.55
  _Scratches("Scratches",Range(0,1))=.5
  _Vignette("Lens falloff",Range(0,1))=.7
  _Bloom("Lamp bloom",Range(0,.5))=.18
 }
 SubShader{Tags{"RenderType"="Opaque" "Queue"="Geometry+20"}Cull Off ZWrite On
 Pass{CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex;float4 _MainTex_ST;float4 _MainTex_TexelSize;
 float _Fps,_Sepia,_Grain,_Flicker,_Weave,_Jolt,_Dust,_Scratches,_Vignette,_Bloom;
 struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;};struct v2f{float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
 v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.uv,_MainTex);return o;}

 float hash11(float p){p=frac(p*.1031);p*=p+33.33;p*=p+p;return frac(p);}
 float hash21(float2 p){float3 q=frac(float3(p.xyx)*.1031);q+=dot(q,q.yzx+33.33);return frac((q.x+q.y)*q.z);}
 float noise21(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
  return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),lerp(hash21(i+float2(0,1)),hash21(i+1),f.x),f.y);}

 // One soft speck; a few per frame land anywhere on the gate.
 float speck(float2 uv,float frame,float n)
 {
  float2 c=float2(hash11(frame*7.1+n*1.7),hash11(frame*3.3+n*5.9));
  float r=lerp(.0015,.007,hash11(frame+n*11.3));
  float2 d=(uv-c)*float2(1.777,1);
  return 1-smoothstep(r*.5,r,length(d+.004*float2(noise21(uv*60+n),noise21(uv*60-n))));
 }
 // A curled hair: a thin band along a wobbling arc.
 float hair(float2 uv,float frame)
 {
  float2 c=float2(hash11(frame*1.9),hash11(frame*4.7));
  float2 d=(uv-c)*float2(1.777,1);float a=atan2(d.y,d.x);
  float rad=.025+.02*hash11(frame*9.1);
  float band=abs(length(d)-rad-.006*sin(a*3+frame));
  return (1-smoothstep(.0004,.0012,band))*step(abs(a-hash11(frame*2.3)*6.28-3.14),1.4);
 }

 fixed4 frag(v2f i):SV_Target
 {
  float frame=floor(_Time.y*_Fps);
  float r0=hash11(frame*.618);

  // Gate weave plus an occasional vertical jolt as a splice passes the gate.
  float2 weave=(float2(noise21(float2(frame*.37,0)),noise21(float2(0,frame*.41)))-.5)*_Weave*2;
  weave.y+=step(1-_Jolt,r0)*(hash11(frame*1.3)-.5)*.03;
  float2 uv=i.uv+weave;

  float3 c=tex2D(_MainTex,uv).rgb;
  // Lamp bloom: a cheap wide tap so highlights halate like a carbon arc.
  float2 t=_MainTex_TexelSize.xy*6;
  float3 halo=(tex2D(_MainTex,uv+float2(t.x,0)).rgb+tex2D(_MainTex,uv-float2(t.x,0)).rgb+tex2D(_MainTex,uv+float2(0,t.y)).rgb+tex2D(_MainTex,uv-float2(0,t.y)).rgb)*.25;
  c+=max(halo-.55,0)*_Bloom*2;

  // Orthochromatic-ish stock: luminance into warm silver.
  float l=dot(c,float3(.30,.59,.11));
  float3 sepia=l*float3(1.07,.95,.78);
  c=lerp(c,sepia,_Sepia);
  c=lerp(.06,1.02,c);

  // Exposure flicker per frame with a slow lamp breathing underneath.
  c*=1+(r0-.5)*_Flicker*2+sin(_Time.y*1.7)*_Flicker*.25;

  // Grain re-rolls every frame.
  float g=noise21(i.uv*float2(900,506)+frame*17.3)-.5;
  g+=.5*(hash21(i.uv*float2(1600,900)+frame)-.5);
  c+=g*_Grain*(.6+.4*(1-l));

  // Scratches: thin bright or dark lines that persist and drift for a few frames.
  float sGroup=floor(frame/6);
  [unroll]for(int k=0;k<3;k++)
  {
   float alive=step(1-_Scratches*.7,hash11(sGroup*3.7+k*13.1));
   float x=hash11(sGroup*9.2+k*4.4)+.003*sin(frame*.9+k);
   float w=lerp(.0006,.0018,hash11(sGroup+k));
   float stroke=1-smoothstep(w*.4,w,abs(uv.x-x));
   stroke*=.5+.5*noise21(float2(k*7,uv.y*24+frame*3));
   c=lerp(c,hash11(k+sGroup)>.4?float3(1,.97,.9):float3(.05,.04,.03),stroke*alive*.7);
  }

  // Dust and hair: dark specks (on the emulsion) and the odd bright one (on the base).
  float dust=0;
  [unroll]for(int n=0;n<6;n++)dust+=speck(uv,frame,n)*step(1-_Dust,hash11(frame*.3+n*2.1));
  float hairs=hair(uv,frame)*step(1-_Dust*.35,hash11(frame*5.5));
  c=lerp(c,r0>.85?float3(.95,.92,.86):float3(.03,.025,.02),saturate(dust+hairs)*.85);

  // Lens falloff and soft gate edges so the image lives inside a projected rectangle.
  float2 q=i.uv-.5;
  float vig=1-_Vignette*smoothstep(.15,.85,dot(q*float2(1.25,1.6),q*float2(1.25,1.6))*2);
  float2 edge=smoothstep(0,.012,i.uv)*smoothstep(0,.012,1-i.uv);
  c*=vig*edge.x*edge.y;

  return fixed4(saturate(c),1);
 }
 ENDCG}}
}
