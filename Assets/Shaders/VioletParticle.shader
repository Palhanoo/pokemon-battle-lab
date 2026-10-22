Shader "Hoenn/VioletParticle"
{
    // Scarlet/Violet effect particles. Their textures are grey masks; colour comes from two animated
    // colours mixed by the mask (the eft2 colour combiner). Per-particle colour0/alpha0 arrive in
    // Custom1, colour1/alpha1 in Custom2 (both HDR). Streams: UV, Custom1, Custom2 packed into TEXCOORD0..2.
    Properties
    {
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
            ZWrite Off
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
                float2 uv = i.t0.xy;
                o.uv.xy = RotateMask(uv,_MainRotation+_MainMotion.z*_EffectTime) * (_MainTex_ST.xy+_MainMotion.xy*_EffectTime) + _MainTex_ST.zw + _MainScroll.xy * _EffectTime;
                o.uv.zw = RotateMask(uv,_SubRotation+_SubMotion.z*_EffectTime) * (_SubTex_ST.xy+_SubMotion.xy*_EffectTime) + _SubTex_ST.zw + _SubScroll.xy * _EffectTime;
                o.thirdUv = RotateMask(uv,_ThirdRotation+_ThirdMotion.z*_EffectTime) * (_ThirdTex_ST.xy+_ThirdMotion.xy*_EffectTime) + _ThirdTex_ST.zw + _ThirdScroll.xy*_EffectTime;
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy);
                if (_MainGrey > 0.5) t = t.rrrr;
                if (_UseSub > 0.5)
                {
                    half4 s = SAMPLE_TEXTURE2D(_SubTex, sampler_SubTex, i.uv.zw);
                    t *= _SubGrey > 0.5 ? s.rrrr : (s.r * s.a).xxxx; // colour masks pack channels; use red
                }
                if (_UseThird > .5)
                {
                    half4 third = SAMPLE_TEXTURE2D(_ThirdTex,sampler_ThirdTex,i.thirdUv);
                    t *= _ThirdGrey > .5 ? third.rrrr : (third.r*third.a).xxxx;
                }
                float3 c0 = i.c0.rgb, c1 = i.c1.rgb;
                float3 col = c0;
                if (_ColorMode > 2.5) col = c0 * t.rgb + c1;
                else if (_ColorMode > 1.5) col = c0 * t.rgb + c1 * (1 - t.rgb);
                else if (_ColorMode > 0.5) col = c0 * t.rgb;
                float a0 = i.c0.a, a1 = i.c1.a, a;
                if (_AlphaMode > 3.5) a = saturate(t.a * a1 - a0); // dissolve: alpha0 is the erosion threshold
                else if (_AlphaMode > 2.5) a = saturate((t.r - (1 - a0)) * 2);
                else if (_AlphaMode > 1.5) a = t.r * a0;
                else if (_AlphaMode > 0.5) a = saturate((t.a - (1 - a0)) * 2);
                else a = t.a * a0;
                // mesh particles get unreliable vertex colour from Unity: tint billboards only
                if (_MeshParticles < 0.5) { col *= i.color.rgb; a *= i.color.a; }
                else col *= 0.55 + 0.45 * saturate(dot(normalize(i.n), normalize(float3(0.3, 1, -0.4)))); // simple form shading for rocks, leaves
                a = saturate(a);
                // Multiplicative effects need white at zero opacity; blend factors ignore alpha.
                if ((_SrcBlend < .5 && abs(_DstBlend-3)<.1) || (abs(_SrcBlend-2)<.1 && _DstBlend<.5)) col=lerp(float3(1,1,1),col,a);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
