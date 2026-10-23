Shader "Hoenn/VioletParticle"
{
    // Scarlet/Violet effect particles. Their textures are grey masks; colour comes from two animated
    // colours mixed by the mask (the eft2 colour combiner). Per-particle colour0/alpha0 arrive in
    // Custom1, colour1/alpha1 in Custom2 (both HDR). Streams: UV, Custom1, Custom2 packed into TEXCOORD0..2.
    Properties
    {
        _BdspDistortion ("Native BDSP distortion mode", Float) = 0
        _DistortionStrength ("Native distortion XY", Vector) = (0,0,0,0)
        _DistortionKey0 ("Distortion life key 0", Vector) = (0,1,0,0)
        _DistortionKey1 ("Distortion life key 1", Vector) = (.25,1,0,0)
        _DistortionKey2 ("Distortion life key 2", Vector) = (.75,1,0,0)
        _DistortionKey3 ("Distortion life key 3", Vector) = (1,1,0,0)
        _NativeFlow ("Native shape/colour/erosion program", Float) = 0
        _FlowColor ("Native flow colour expression", Float) = 0
        _FlowShape ("Native edge width, colour power, shape power, opacity", Vector) = (0,1,1,1)
        _FlowEdge ("Native edge colour", Vector) = (1,1,1,1)
        _Fresnel ("Native Fresnel enabled, start, end", Vector) = (0,0,1,0)
        _ZTest ("Native depth comparison", Float) = 4
        _AlphaTest ("Native alpha comparison enabled", Float) = 0
        _AlphaCompare ("Native alpha comparison", Float) = 4
        _AlphaThreshold ("Native cutout threshold", Float) = 0
        _PrimitiveStream ("Native primitive channels in UV2/UV3", Float) = 0
        _OpaqueParticle ("Unblended native surface", Float) = 0
        _Refraction ("Native distortion layer", Float) = 0
        _RefractionScale ("Screen displacement calibration", Float) = 0.015
        _DistortionByDistance ("Attenuate distortion by camera distance", Float) = 0
        _SoftParticle ("Soft intersection: enabled / near / span", Vector) = (0,0,1,0)
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
        _MainUvChannel ("Main native UV channel", Float) = 0
        _SubUvChannel ("Second native UV channel", Float) = 0
        _ThirdUvChannel ("Third native UV channel", Float) = 0
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
        _ParticleUvClock ("Use native particle texture clock", Float) = 0
        _MainSheet ("Main texture atlas dimensions", Vector) = (1,1,0,0)
        _MainRandomScroll ("Main random texture phase", Vector) = (0,0,0,0)
        _SubRandomScroll ("Sub random texture phase", Vector) = (0,0,0,0)
        _ThirdRandomScroll ("Third random texture phase", Vector) = (0,0,0,0)
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
            ZTest [_ZTest]
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_SubTex); SAMPLER(sampler_SubTex);
            TEXTURE2D(_ThirdTex); SAMPLER(sampler_ThirdTex);
            CBUFFER_START(UnityPerMaterial)
                float _BdspDistortion;
                float4 _DistortionStrength,_DistortionKey0,_DistortionKey1,_DistortionKey2,_DistortionKey3;
                float _NativeFlow,_FlowColor;
                float4 _FlowShape,_FlowEdge,_Fresnel;
                float _MainUvChannel, _SubUvChannel, _ThirdUvChannel;
                float _ZTest, _AlphaTest, _AlphaCompare, _AlphaThreshold, _OpaqueParticle, _PrimitiveStream;
                float _Refraction, _RefractionScale, _DistortionByDistance;
                float4 _SoftParticle;
                float _NativeCombiner, _Model, _ZWrite;
                float4 _ColorChannels, _AlphaChannels, _ColorBlends, _AlphaBlends, _ModelColor, _ModelEmission;
                float4 _MainWrap, _SubWrap, _ThirdWrap;
                float _ReadabilityAlpha, _MainSphere, _SubSphere, _ThirdSphere;
                float _ParticleUvClock;
                float4 _MainSheet,_MainRandomScroll,_SubRandomScroll,_ThirdRandomScroll;
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
                float4 t2 : TEXCOORD2; // c1.zw, primitive.rg
                float4 t3 : TEXCOORD3; // primitive.ba, normalized age, inverse lifetime
                float2 t5 : TEXCOORD5; // BDSP secondary mesh UV, after primitive and particle streams
                float4 t4 : TEXCOORD4; // stable per-particle random
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
                float3 world : TEXCOORD5;
                float2 rawUv : TEXCOORD6;
                float4 primitive : TEXCOORD7;
                float4 particleUv : TEXCOORD8; // seconds since birth, random phase
                float age : TEXCOORD9;
                float2 sourceUv1 : TEXCOORD10;
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
                o.sourceUv1=i.t5;
                o.world=TransformObjectToWorld(i.pos.xyz);o.rawUv=i.t0.xy;
                o.cs = TransformWorldToHClip(o.world);
                o.color = i.color;
                o.n = TransformObjectToWorldNormal(i.normal);
                o.c0 = float4(i.t0.zw, i.t1.xy);
                o.c1 = float4(i.t1.zw, i.t2.xy);
                o.primitive=float4(i.t2.zw,i.t3.xy);
                o.particleUv=float4(i.t3.z/max(i.t3.w,1e-6),i.t4.xyz);o.age=i.t3.z;
                if(_Model>.5){o.c0=_ModelColor;o.c1=float4(0,0,0,1);o.color=1;}
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
            float AlphaChannel(float4 t,float channel){return channel<.5?t.a:channel<1.5?t.r:channel<2.5?1:channel<3.5?1-t.a:1-t.r;}
            float4 Combine(float4 a,float4 b,float colorOp,float alphaOp)
            {return float4(colorOp<.5?a.rgb*b.rgb:colorOp<1.5?a.rgb+b.rgb:a.rgb-b.rgb,alphaOp<.5?a.a*b.a:alphaOp<1.5?a.a+b.a:a.a-b.a);}
            float4 Channels(float4 t,float color,float alpha){return float4(ColorChannel(t,color),AlphaChannel(t,alpha));}
            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Normalize after interpolation: sphere masks must not flatten across coarse mesh faces.
                float2 sphereUv=normalize(mul((float3x3)GetWorldToViewMatrix(),i.n)).xy*.5+.5;
                float clock=lerp(_EffectTime,i.particleUv.x,_ParticleUvClock);
                // Unity animates UV0 for the main atlas. Other texture slots use the
                // full original face, not the narrow rectangle of that atlas frame.
                float2 baseUv=i.rawUv;
                if(_ParticleUvClock>.5&&(_MainSheet.x>1||_MainSheet.y>1))baseUv=frac(i.rawUv*_MainSheet.xy);
                float2 phase=i.particleUv.yz*_ParticleUvClock;
                i.uv.xy=RotateMask(lerp(lerp(i.rawUv,i.sourceUv1,_MainUvChannel),sphereUv,_MainSphere),_MainRotation+_MainMotion.z*clock)*(_MainTex_ST.xy+_MainMotion.xy*clock)+_MainTex_ST.zw+_MainScroll.xy*clock+phase*_MainRandomScroll.xy;
                i.uv.zw=RotateMask(lerp(lerp(baseUv,i.sourceUv1,_SubUvChannel),sphereUv,_SubSphere),_SubRotation+_SubMotion.z*clock)*(_SubTex_ST.xy+_SubMotion.xy*clock)+_SubTex_ST.zw+_SubScroll.xy*clock+phase.yx*_SubRandomScroll.xy;
                i.thirdUv=RotateMask(lerp(lerp(baseUv,i.sourceUv1,_ThirdUvChannel),sphereUv,_ThirdSphere),_ThirdRotation+_ThirdMotion.z*clock)*(_ThirdTex_ST.xy+_ThirdMotion.xy*clock)+_ThirdTex_ST.zw+_ThirdScroll.xy*clock+frac(phase+.381966)*_ThirdRandomScroll.xy*_ParticleUvClock;
                float2 screenUv=GetNormalizedScreenSpaceUV(i.cs);
                float eyeDepth=-TransformWorldToView(i.world).z;
                float fade=1;
                if(_SoftParticle.x>.5)
                {
                    float sceneDepth=LinearEyeDepth(SampleSceneDepth(screenUv),_ZBufferParams);
                    fade=saturate((sceneDepth-eyeDepth-_SoftParticle.y)/_SoftParticle.z);
                }
                float2 da=i.age<_DistortionKey1.x?_DistortionKey0.xy:i.age<_DistortionKey2.x?_DistortionKey1.xy:_DistortionKey2.xy;
                float2 db=i.age<_DistortionKey1.x?_DistortionKey1.xy:i.age<_DistortionKey2.x?_DistortionKey2.xy:_DistortionKey3.xy;
                float distortionLife=lerp(da.y,db.y,saturate((i.age-da.x)/max(.0001,db.x-da.x)));
                float4 distortionVector=SAMPLE_TEXTURE2D(_SubTex,sampler_SubTex,WrapMask(i.uv.zw,_SubWrap.xy));
                if(_BdspDistortion>1.5)i.uv.xy+=(distortionVector.rg*2-1)*_DistortionStrength.xy*distortionLife;
                if(_NativeFlow>2.5&&_NativeFlow<3.5)
                {
                    // Authored beam turbulence shears the two flowing maps along the tube.
                    i.uv.z+=i.rawUv.y*_FlowShape.z;
                    i.thirdUv.x+=i.rawUv.y*_FlowShape.z;
                }
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, WrapMask(i.uv.xy,_MainWrap.xy));
                half4 distortionMask=t;
                float4 flowMain=t, flowSub=1, flowNoise=1;
                if (_MainGrey > 0.5) t = t.rrrr;
                if(_NativeCombiner>.5)t=Channels(t,_ColorChannels.x,_AlphaChannels.x);
                if (_UseSub > 0.5)
                {
                    half4 s = SAMPLE_TEXTURE2D(_SubTex, sampler_SubTex, WrapMask(i.uv.zw,_SubWrap.xy));
                    flowSub=s;
                    if(_SubGrey>.5)s=s.rrrr;
                    if(_NativeCombiner>.5)t=Combine(t,Channels(s,_ColorChannels.y,_AlphaChannels.y),_ColorBlends.x,_AlphaBlends.x);
                    else t *= _SubGrey > .5 ? s.rrrr : (s.r*s.a).xxxx;
                }
                if (_UseThird > .5)
                {
                    half4 third = SAMPLE_TEXTURE2D(_ThirdTex,sampler_ThirdTex,WrapMask(i.thirdUv,_ThirdWrap.xy));
                    flowNoise=third;
                    if(_ThirdGrey>.5)third=third.rrrr;
                    if(_NativeCombiner>.5)t=Combine(t,Channels(third,_ColorChannels.z,_AlphaChannels.z),_ColorBlends.y,_AlphaBlends.y);
                    else t *= _ThirdGrey > .5 ? third.rrrr : (third.r*third.a).xxxx;
                }
                if(_NativeCombiner>.5 && _PrimitiveStream>.5 && _Model<.5 && _Refraction<.5)
                    t=Combine(t,Channels(i.primitive,_ColorChannels.w,_AlphaChannels.w),_ColorBlends.z,_AlphaBlends.z);
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
                if(_NativeFlow>.5&&_NativeFlow<1.5)
                {
                    // Reconstructed from the asset's embedded NV fragment program,
                    // CSDP offsets 48/64. Secondary maps must not multiply coverage.
                    float shape=pow(max(0,flowMain.r),_FlowShape.z);
                    float coverage=pow(max(0,shape*(1+flowNoise.r)*.5),.675);
                    float width=_FlowShape.x;
                    float threshold=coverage-width+a0*.5-1;
                    float interior=ceil(saturate(threshold));
                    float edge=ceil(threshold+width-.0001)*(1-interior);
                    float3 fill=lerp(c1,c0,pow(max(0,flowSub.r),_FlowShape.y));
                    if(_FlowColor>.5)
                        fill=(c0*(1-abs(_FlowShape.y))+c0*flowSub.rgb*saturate(-_FlowShape.y)+(c0+flowSub.rgb)*saturate(_FlowShape.y));
                    col=lerp(_FlowEdge.rgb*_FlowEdge.a,fill,interior);
                    if(_FlowColor>.5)col*=c1;
                    a=saturate(a1*(interior*_FlowShape.w+edge));
                }
                if(_NativeFlow>1.5)
                {
                    float noise=flowNoise.r+a0-1;
                    float inside=ceil(saturate(noise-_FlowShape.x));
                    float visible=ceil(saturate(noise));
                    float3 left=_NativeFlow>3.5?lerp(c1,c0,flowMain.r):_NativeFlow<2.5?c0*flowMain.rgb:c0;
                    float3 right=_NativeFlow<2.5?c1*flowSub.rgb:flowSub.rgb;
                    float3 fill=left*(1-abs(_FlowShape.y))+left*right*saturate(-_FlowShape.y)+(left+right)*saturate(_FlowShape.y);
                    col=lerp(_FlowEdge.rgb*_FlowEdge.a,fill,inside);
                    if(_NativeFlow<2.5||_NativeFlow>3.5)
                    {
                        float facing=abs(dot(normalize(i.n),normalize(GetWorldSpaceViewDir(i.world))));
                        float f=saturate((facing-_Fresnel.y)/((_Fresnel.z-_Fresnel.y)==0?.0001:(_Fresnel.z-_Fresnel.y)));
                        float rim=pow(max(0,1-f*f*(3-2*f)),_FlowShape.z);
                        col+=rim*_FlowShape.w*_FlowEdge.rgb;
                        a=a1*visible;
                    }
                    else {col*=c1;a=flowMain.r*visible*a1;}
                }
                if(_Fresnel.x>.5&&(_NativeFlow<1.5||(_NativeFlow>2.5&&_NativeFlow<3.5)))
                {
                    float facing=abs(dot(normalize(i.n),normalize(GetWorldSpaceViewDir(i.world))));
                    float rim=saturate((facing-_Fresnel.y)/((_Fresnel.z-_Fresnel.y)==0?.0001:(_Fresnel.z-_Fresnel.y)));
                    a*=rim*rim*(3-2*rim);
                }
                // Native particle tint is distinct from primitive channels; model draws opt out.
                if (_MeshParticles < 0.5) { col *= i.color.rgb; a *= i.color.a; }
                else if(_Model>.5) col *= 0.55 + 0.45 * saturate(dot(normalize(i.n), normalize(float3(0.3, 1, -0.4)))); // simple form shading for rocks, leaves
                if(_Model>.5)col+=_ModelEmission.rgb;
                a*=fade;
                if(_AlphaTest>.5)
                {
                    float delta=a-_AlphaThreshold;
                    bool alphaPass=_AlphaCompare<.5?false:_AlphaCompare<1.5?delta<0:_AlphaCompare<2.5?abs(delta)<.00001:_AlphaCompare<3.5?delta<=0:_AlphaCompare<4.5?delta>0:_AlphaCompare<5.5?abs(delta)>.00001:_AlphaCompare<6.5?delta>=0:true;
                    clip(alphaPass?1:-1);
                }
                a = saturate(a)*_ReadabilityAlpha;
                if(_Refraction>.5)
                {
                    // Signed RG vectors are data, never visible colour. URP screen-space calibration
                    // replaces the original engine's framebuffer scale; native lifetime curve is c1.z.
                    float strength=max(0,lerp(i.c1.z,1,_Model))*_RefractionScale;
                    if(_DistortionByDistance>.5)strength/=max(1,eyeDepth);
                    float2 distortionDirection=(distortionMask.rg*2-1);
                    if(_BdspDistortion>.5){distortionDirection=(distortionVector.rg*2-1)*_DistortionStrength.xy;strength=distortionLife*_RefractionScale;}
                    float2 offset=distortionDirection*strength*a;
                    offset.x*=_ScreenParams.y/_ScreenParams.x;
                    float2 displaced=clamp(screenUv+offset,0.001,0.999);
                    // Do not pull foreground silhouettes across the refracting surface.
                    float displacedDepth=LinearEyeDepth(SampleSceneDepth(displaced),_ZBufferParams);
                    if(displacedDepth<eyeDepth)displaced=screenUv;
                    // Fade the mesh silhouette without tinting it, including at zero strength.
                    float facing=abs(dot(normalize(i.n),normalize(GetWorldSpaceViewDir(i.world))));
                    float edge=_MeshParticles>.5?smoothstep(0,.25,facing):1;
                    return half4(SampleSceneColor(displaced),a*edge*saturate(strength*10000));
                }
                if(abs(_SrcBlend-1)<.1&&_OpaqueParticle<.5)col*=a;
                // Multiplicative effects need white at zero opacity; blend factors ignore alpha.
                if ((_SrcBlend < .5 && abs(_DstBlend-3)<.1) || (abs(_SrcBlend-2)<.1 && _DstBlend<.5)) col=lerp(float3(1,1,1),col,a);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
