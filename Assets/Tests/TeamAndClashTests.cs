using System.Linq;
using BattleLab;
using BattleLab.Native;
using NUnit.Framework;
using UnityEngine;
public class TeamAndClashTests
{
    [TestCase(100,100,0,0)][TestCase(149,100,0,0)][TestCase(150,100,50,0)]
    [TestCase(100,150,0,50)][TestCase(0,100,0,100)][TestCase(0,0,0,0)]
    public void ClashThresholdAndResidual(int a,int b,int expectedA,int expectedB)
    {var r=ClashRules.Resolve(a,b);Assert.AreEqual(expectedA,r.a);Assert.AreEqual(expectedB,r.b);}
    [Test] public void SweptProjectilesCannotTunnelOrCollideAtDifferentTimes()
    {
        Assert.IsTrue(ClashRules.SweptContact(Vector3.left,Vector3.right,Vector3.right,Vector3.left,.1f,out var p));Assert.Less(p.magnitude,.01f);
        Assert.IsFalse(ClashRules.SweptContact(Vector3.left*3,Vector3.right,Vector3.back,Vector3.forward*3,.1f,out _));
        Assert.IsFalse(ClashRules.SweptContact(Vector3.left,Vector3.right,Vector3.right+Vector3.up,Vector3.left+Vector3.up,.1f,out _));
    }
    [Test] public void ClashUsesDamageAfterStabAndTargetType()
    {
        var c=Resources.Load<LabCatalog>("LabCatalog");var fire=c.Species("TORCHIC");var water=c.Species("MUDKIP");
        var flame=c.Move("FLAMETHROWER");var gun=c.Move("WATER_GUN");
        int a=CombatRules.Damage(fire,water,flame,c.data.types,BattleMode.ZA,MoveStyle.Normal);
        int b=CombatRules.Damage(water,fire,gun,c.data.types,BattleMode.ZA,MoveStyle.Normal);
        Assert.Greater(b,a,"STAB and super-effective water must be included before comparing nominal powers");
        Assert.Less(b,a*1.5f,"Type advantage alone does not guarantee the clash threshold");
        var result=ClashRules.Resolve(a,b);Assert.AreEqual(0,result.a);Assert.AreEqual(0,result.b);
    }
    [Test] public void NativeStopClearsInfiniteAuraCoresButLetsFiniteParticlesAge()
    {
        var cat=Resources.Load<VioletFxCatalog>("NativeZA");var set=cat.moves.Single(m=>m.id=="MOVE_AURA_SPHERE").sets.Single(s=>s.name=="ew0396_charge");
        var root=VioletFx.Build(set.text,n=>cat.textures.FirstOrDefault(t=>t.name==n),cat.material);
        try
        {
            foreach(var ps in VioletFx.Roots(root))ps.Simulate(1.1f,true,true,false);
            var infinite=root.GetComponentsInChildren<VioletInfiniteEmitter>().Select(x=>x.GetComponent<ParticleSystem>()).ToArray();
            Assert.IsNotEmpty(infinite);Assert.Greater(infinite.Sum(p=>p.particleCount),0);
            VioletFx.Stop(root,true);Assert.AreEqual(0,infinite.Sum(p=>p.particleCount));
            foreach(var ps in VioletFx.Roots(root))ps.Simulate(.4f,true,false,false);
            Assert.AreEqual(0,infinite.Sum(p=>p.particleCount),"stopped cores must not respawn");
        }
        finally{foreach(var r in root.GetComponentsInChildren<ParticleSystemRenderer>())Object.DestroyImmediate(r.sharedMaterial);Object.DestroyImmediate(root);}
    }
    [Test] public void EveryNativeZAEmitterBindsEverySupportedTextureSlot()
    {
        var cat=Resources.Load<VioletFxCatalog>("NativeZA");int checkedLayers=0;
        foreach(var set in cat.moves.SelectMany(m=>m.sets).GroupBy(s=>s.name).Select(g=>g.First()))
        {
            var root=VioletFx.Build(set.text,n=>cat.textures.FirstOrDefault(t=>t.name==n),cat.material);
            try
            {
                var data=VioletFx.J.Obj(MiniJson.Parse(set.text));
                foreach(var group in VioletFx.J.List(data,"sets"))foreach(var e in VioletFx.J.List(VioletFx.J.Obj(group),"emitters"))Check(VioletFx.J.Obj(e),root.transform);
                void Check(System.Collections.Generic.Dictionary<string,object> emitter,Transform parent)
                {
                    var ps=parent.GetComponentsInChildren<ParticleSystem>(true).First(p=>p.name==VioletFx.J.Str(emitter,"name"));
                    var material=ps.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                    foreach(var item in VioletFx.J.List(emitter,"textures"))
                    {
                        var tex=VioletFx.J.Obj(item);if(VioletFx.J.B(tex,"sphere_map")||VioletFx.J.F(tex,"slot")>2)continue;
                        string name=VioletFx.J.Str(tex,"name");
                        Assert.IsTrue(new[]{"_MainTex","_SubTex","_ThirdTex"}.Any(p=>material.GetTexture(p)&&material.GetTexture(p).name==name),set.name+"/"+ps.name+" missing "+name);checkedLayers++;
                    }
                    foreach(var child in VioletFx.J.List(emitter,"children"))Check(VioletFx.J.Obj(child),ps.transform);
                }
            }
            finally
            {
                foreach(var r in root.GetComponentsInChildren<ParticleSystemRenderer>()){Object.DestroyImmediate(r.sharedMaterial);if(r.mesh&&r.mesh.name.StartsWith("violet primitive"))Object.DestroyImmediate(r.mesh);}
                Object.DestroyImmediate(root);
            }
        }
        Assert.Greater(checkedLayers,200);
    }
    [Test] public void EveryNativeBDSPMaterialBindsBothExportedTextureLayers()
    {
        var cat=Resources.Load<BdspFxCatalog>("NativeBDSP");int checkedLayers=0;
        foreach(var prefab in cat.prefabs)
        {
            var root=BdspFx.Build(prefab.text,n=>cat.textures.FirstOrDefault(t=>t.name==n),cat.material);
            try
            {
                var data=VioletFx.J.Obj(MiniJson.Parse(prefab.text));
                foreach(var item in VioletFx.J.List(data,"nodes"))
                {
                    var node=VioletFx.J.Obj(item);if(!node.TryGetValue("r",out var r))continue;
                    var render=VioletFx.J.Obj(r);if(!render.TryGetValue("mat",out var m))continue;
                    var mat=VioletFx.J.Obj(m);var renderer=root.GetComponentsInChildren<ParticleSystemRenderer>(true).First(x=>x.name==VioletFx.J.Str(node,"name"));
                    foreach(var pair in new[]{("tex0","_MainTex"),("tex1","_SubTex")})
                    {
                        if(!mat.TryGetValue(pair.Item1,out var value)||value==null)continue;
                        string name=VioletFx.J.Str(VioletFx.J.Obj(value),"name");
                        Assert.AreEqual(name,renderer.sharedMaterial.GetTexture(pair.Item2)?.name,prefab.name+"/"+renderer.name);checkedLayers++;
                    }
                }
            }
            finally{foreach(var r in root.GetComponentsInChildren<ParticleSystemRenderer>())if(r.sharedMaterial)Object.DestroyImmediate(r.sharedMaterial);Object.DestroyImmediate(root);}
        }
        Assert.Greater(checkedLayers,100);
    }
}
