Shader "Hoenn/VioletParticle"
{
    // Scarlet/Violet effect particles. Their textures are grey masks; colour comes from two animated
    // colours mixed by the mask (the eft2 colour combiner). Per-particle colour0/alpha0 arrive in
    // Custom1, colour1/alpha1 in Custom2 (both HDR). Streams: UV, Custom1, Custom2 packed into TEXCOORD0..2.
    Properties
    {
        _NativeCombiner ("Native channel combiner", Float) = 0
        _ColorChannels ("Texture colour channels", Vector) = (0,0,0,0)
        _AlphaChannels ("Texture alpha channels", Vector) = (0,0,0,0)
        _ColorBlends ("Texture colour operations", Vector) = (0,0,0,0)
        _AlphaBlends ("Texture alpha operations", Vector) = (0,0,0,0)
        _Model ("Ordinary model vertex stream", Float) = 0
        _ModelColor ("Model colour", Color) = (1,1,1,1)
        _ModelEmission ("Model emission", Color) = (0,0,0,0)
        _ZWrite ("Depth write", Float) = 0
        _MainWrap ("Main wrap", Vector) = (1,1,0,0)
        _SubWrap ("Second wrap", Vector) = (1,1,0,0)
        _ThirdWrap ("Third wrap", Vector) = (1,1,0,0)
        _MainSphere ("Main sphere projection", Float) = 0
        _SubSphere ("Second sphere projection", Float) = 0
        _ThirdSphere ("Third sphere projection", Float) = 0
        _ReadabilityAlpha ("Presentation opacity", Range(0,1)) = 1
        _ThirdTex ("Third mask", 2D) = "white" {}
        _ThirdScroll ("Third mask scroll", Vector) = (0,0,0,0)
        _ThirdRotation ("Third mask rotation", Float) = 0
        _UseThird ("Use third mask", Float) = 0
        _ThirdGrey ("Third mask is single channel", Float) = 0
        _MainMotion ("Main scale / rotation rate", Vector) = (0,0,0,0)
        _SubMotion ("Second scale / rotation rate", Vector) = (0,0,0,0)
        _ThirdMotion ("Third scale / rotation rate", Vector) = (0,0,0,0)
        _EffectTime ("Presentation clock", Float) = 0
        _MainScroll ("Main mask scroll", Vector) = (0,0,0,0)
        _MainRotation ("Main mask rotation", Float) = 0
        _SubRotation ("Second mask rotation", Float) = 0
        _MainTex ("Mask", 2D) = "white" {}
        _SubTex ("Second mask", 2D) = "white" {}
        _SubScroll ("Second mask scroll per second", Vector) = (0, 0, 0, 0)
        _UseSub ("Use second mask", Float) = 0
        _MeshParticles ("Mesh particles", Float) = 0
        _MainGrey ("Mask is single channel", Float) = 0
        _SubGrey ("Second mask is single channel", Float) = 0
        _ColorMode ("Colour mode", Float) = 2
        _AlphaMode ("Alpha mode", Float) = 0
        _SrcBlend ("Src blend", Float) = 5
        _DstBlend ("Dst blend", Float) = 10
        _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_SubTex); SAMPLER(sampler_SubTex);
            TEXTURE2D(_ThirdTex); SAMPLER(sampler_ThirdTex);
            CBUFFER_START(UnityPerMaterial)
                float _NativeCombiner, _Model, _ZWrite;
                float4 _ColorChannels, _AlphaChannels, _ColorBlends, _AlphaBlends, _ModelColor, _ModelEmission;
                float4 _MainWrap, _SubWrap, _ThirdWrap;
                float _ReadabilityAlpha, _MainSphere, _SubSphere, _ThirdSphere;
                float _EffectTime, _MainRotation, _SubRotation, _ThirdRotation, _UseThird, _ThirdGrey;
                float4 _ThirdTex_ST, _ThirdScroll, _MainMotion, _SubMotion, _ThirdMotion;
                float4 _MainScroll;
                float4 _MainTex_ST, _SubTex_ST, _SubScroll;
                float _MainGrey, _SubGrey, _MeshParticles, _UseSub, _ColorMode, _AlphaMode, _SrcBlend, _DstBlend, _Cull;
            CBUFFER_END
            struct A
            {
                float4 pos : POSITION;
                float3 normal : NORMAL;
                half4 color : COLOR;
                float4 t0 : TEXCOORD0; // uv.xy, c0.xy
                float4 t1 : TEXCOORD1; // c0.zw, c1.xy
                float2 t2 : TEXCOORD2; // c1.zw
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct V
            {
                float4 cs : SV_POSITION;
                half4 color : COLOR;
                float4 c0 : TEXCOORD0;
                float4 c1 : TEXCOORD1;
                float4 uv : TEXCOORD2;
                float3 n : TEXCOORD3;
                float2 thirdUv : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            float2 RotateMask(float2 uv,float angle)
            {float sn,cs;sincos(angle,sn,cs);uv-=.5;return float2(cs*uv.x-sn*uv.y,sn*uv.x+cs*uv.y)+.5;}
            V vert(A i)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.cs = TransformObjectToHClip(i.pos.xyz);
                o.color = i.color;
                o.n = TransformObjectToWorldNormal(i.normal);
                o.c0 = float4(i.t0.zw, i.t1.xy);
                o.c1 = float4(i.t1.zw, i.t2);
                if(_Model>.5){o.c0=_ModelColor;o.c1=float4(0,0,0,1);o.color=1;}
                float2 uv = i.t0.xy;
                float3 viewNormal=normalize(mul((float3x3)GetWorldToViewMatrix(),o.n));
                float2 sphereUv=viewNormal.xy*.5+.5;
                float2 mainUv=lerp(uv,sphereUv,_MainSphere);
                float2 subUv=lerp(uv,sphereUv,_SubSphere);
                float2 thirdUv=lerp(uv,sphereUv,_ThirdSphere);
                o.uv.xy = RotateMask(mainUv,_MainRotation+_MainMotion.z*_EffectTime) * (_MainTex_ST.xy+_MainMotion.xy*_EffectTime) + _MainTex_ST.zw + _MainScroll.xy * _EffectTime;
                o.uv.zw = RotateMask(subUv,_SubRotation+_SubMotion.z*_EffectTime) * (_SubTex_ST.xy+_SubMotion.xy*_EffectTime) + _SubTex_ST.zw + _SubScroll.xy * _EffectTime;
                o.thirdUv = RotateMask(thirdUv,_ThirdRotation+_ThirdMotion.z*_EffectTime) * (_ThirdTex_ST.xy+_ThirdMotion.xy*_EffectTime) + _ThirdTex_ST.zw + _ThirdScroll.xy*_EffectTime;
                return o;
            }
            float WrapAxis(float uv,float mode)
            {
                if(mode<.5)return frac(uv);
                if(mode<1.5)return saturate(uv);
                if(mode<2.5)return 1-abs(frac(uv*.5)*2-1);
                return saturate(abs(uv));
            }
            float2 WrapMask(float2 uv,float2 mode){return float2(WrapAxis(uv.x,mode.x),WrapAxis(uv.y,mode.y));}
            float3 ColorChannel(float4 t,float channel){return channel<.5?t.rgb:channel<1.5?float3(1,1,1):channel<2.5?1-t.rgb:channel<3.5?t.aaa:1-t.aaa;}
            float AlphaChannel(float4 t,float channel){return channel<.5?t.a:channel<1.5?t.r:channel<2.5?1:1-t.a;}
            float4 Combine(float4 a,float4 b,float colorOp,float alphaOp)
            {return float4(colorOp<.5?a.rgb*b.rgb:colorOp<1.5?a.rgb+b.rgb:a.rgb-b.rgb,alphaOp<.5?a.a*b.a:alphaOp<1.5?a.a+b.a:a.a-b.a);}
            float4 Channels(float4 t,float color,float alpha){return float4(ColorChannel(t,color),AlphaChannel(t,alpha));}
            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, WrapMask(i.uv.xy,_MainWrap.xy));
                if (_MainGrey > 0.5) t = t.rrrr;
                if(_NativeCombiner>.5)t=Channels(t,_ColorChannels.x,_AlphaChannels.x);
                if (_UseSub > 0.5)
                {
                    half4 s = SAMPLE_TEXTURE2D(_SubTex, sampler_SubTex, WrapMask(i.uv.zw,_SubWrap.xy));
                    if(_SubGrey>.5)s=s.rrrr;
                    if(_NativeCombiner>.5)t=Combine(t,Channels(s,_ColorChannels.y,_AlphaChannels.y),_ColorBlends.x,_AlphaBlends.x);
                    else t *= _SubGrey > .5 ? s.rrrr : (s.r*s.a).xxxx;
                }
                if (_UseThird > .5)
                {
                    half4 third = SAMPLE_TEXTURE2D(_ThirdTex,sampler_ThirdTex,WrapMask(i.thirdUv,_ThirdWrap.xy));
                    if(_ThirdGrey>.5)third=third.rrrr;
                    if(_NativeCombiner>.5)t=Combine(t,Channels(third,_ColorChannels.z,_AlphaChannels.z),_ColorBlends.y,_AlphaBlends.y);
                    else t *= _ThirdGrey > .5 ? third.rrrr : (third.r*third.a).xxxx;
                }
                float3 c0 = i.c0.rgb, c1 = i.c1.rgb;
                float3 col = c0;
                if (_ColorMode > 2.5) col = c0 * t.rgb + c1;
                else if (_ColorMode > 1.5) col = c0 * t.rgb + c1 * (1 - t.rgb);
                else if (_ColorMode > 0.5) col = c0 * t.rgb;
                float a0 = i.c0.a, a1 = i.c1.a, a;
                if (_AlphaMode > 5.5) a=t.a*a0*a1;
                else if (_AlphaMode > 4.5) a=(t.a-a0)*a1;
                else if (_AlphaMode > 3.5) a = saturate((t.a-a0)*4)*a1; // native erosion, followed by opacity

                else if (_AlphaMode > 2.5) a = saturate((t.r - (1 - a0)) * 2);
                else if (_AlphaMode > 1.5) a = t.r * a0;
                else if (_AlphaMode > 0.5) a = saturate((t.a - (1 - a0)) * 2);
                else a = t.a * a0;
                // mesh particles get unreliable vertex colour from Unity: tint billboards only
                if (_MeshParticles < 0.5) { col *= i.color.rgb; a *= i.color.a; }
                else col *= 0.55 + 0.45 * saturate(dot(normalize(i.n), normalize(float3(0.3, 1, -0.4)))); // simple form shading for rocks, leaves
                if(_Model>.5)col+=_ModelEmission.rgb;
                a = saturate(a)*_ReadabilityAlpha;
                if(abs(_SrcBlend-1)<.1)col*=a;
                // Multiplicative effects need white at zero opacity; blend factors ignore alpha.
                if ((_SrcBlend < .5 && abs(_DstBlend-3)<.1) || (abs(_SrcBlend-2)<.1 && _DstBlend<.5)) col=lerp(float3(1,1,1),col,a);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
