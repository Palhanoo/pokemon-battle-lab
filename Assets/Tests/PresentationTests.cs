using System;
using System.Collections.Generic;
using System.Linq;
using BattleLab;
using BattleLab.Native;
using NUnit.Framework;
using UnityEngine;

public class PresentationTests
{
    [TestCase("Arceus","PIKACHU")][TestCase("Arceus","GENGAR")][TestCase("Arceus","LUCARIO")]
    [TestCase("ZA","PIKACHU")][TestCase("ZA","GENGAR")][TestCase("ZA","LUCARIO")]
    [TestCase("BDSP","PIKACHU")][TestCase("BDSP","GENGAR")][TestCase("BDSP","LUCARIO")]
    [TestCase("ZA","TREECKO")][TestCase("ZA","TORCHIC")][TestCase("ZA","MUDKIP")]
    [TestCase("ZA","RALTS")][TestCase("SV","POOCHYENA")][TestCase("BDSP","BRELOOM")]
    public void EveryLoadoutHasEffectsAndARecoveryPlan(string source,string species)
    {
        var cat=Resources.Load<LabCatalog>("LabCatalog");var table=Resources.Load<NativeTiming>("NativeTiming");
        var model=cat.Model(species,source);
        foreach(var id in cat.Species(species).moves)
        {
            Assert.IsTrue(Resources.Load<VioletFxCatalog>("NativeZA").Has(id)||Resources.Load<BdspFxCatalog>("NativeBDSP").Has(id),id);
            // BDSP uses the shared native fallback for moves without a BDSP variant.
            var plan=new List<(float at,AnimationClip clip,float speed)>();
            float end=source=="BDSP"?AttackPresentation.SplitPlan(model,model.special,0,1,plan):ZaTimeline.Plan(table.Get(id),model,cat.Move(id).physical?"Physical":"Special",plan);
            Assert.That(end,Is.GreaterThan(0).And.LessThan(12),source+id);
            Assert.Greater(plan.Count,1);Assert.IsTrue(plan.All(x=>x.clip&&x.speed>0));
            Assert.AreSame(model.battle,plan.Last().clip,"must release body ownership into idle");
            Assert.IsTrue(plan.Zip(plan.Skip(1),(a,b)=>b.at>=a.at).All(x=>x),"ordered cues");
        }
    }
    [Test] public void ArceusPikachuSplitSpecialCannotHoldItsStartPose()
    {
        var model=Resources.Load<LabCatalog>("LabCatalog").Model("PIKACHU","Arceus");
        Assert.AreEqual("rangeattack01_start",model.special.name);
        var cues=new List<(float at,AnimationClip clip,float speed)>();
        float release=model.special.length+1;
        AttackPresentation.SplitPlan(model,model.special,0,release,cues);
        CollectionAssert.AreEqual(new[]{"rangeattack01_start","rangeattack01_loop","rangeattack01_end",model.battle.name},cues.Select(c=>c.clip.name).ToArray());
        Assert.AreEqual(release,cues[2].at,.001f);
    }
    [Test] public void NativePikachuQuickAttackReleasesSplitRush()
    {
        var model=Resources.Load<LabCatalog>("LabCatalog").Model("PIKACHU","ZA");
        var cues=new List<(float at,AnimationClip clip,float speed)>();
        ZaTimeline.Plan(Resources.Load<NativeTiming>("NativeTiming").Get("QUICK_ATTACK"),model,"Physical",cues);
        Assert.IsTrue(cues.Any(c=>c.clip.name=="attack03_start"));
        Assert.IsTrue(cues.Any(c=>c.clip.name=="attack03_end"));
        Assert.AreSame(model.battle,cues.Last().clip);
    }
    [Test] public void EffectTexturesAndPrefabsAreActuallyReferenced()
    {
        var za=Resources.Load<VioletFxCatalog>("NativeZA");var bdsp=Resources.Load<BdspFxCatalog>("NativeBDSP");
        Assert.AreEqual(Resources.Load<LabCatalog>("LabCatalog").data.moves.Count(m=>m.effectOrigin!="BDSP")+1,za.moves.Count);Assert.GreaterOrEqual(bdsp.moves.Count,22);
        Assert.Greater(za.textures.Count,50);Assert.Greater(bdsp.textures.Count,50);
        Assert.IsFalse(za.textures.Any(t=>!t));Assert.IsFalse(bdsp.prefabs.Any(p=>!p));
        foreach(var m in za.moves)Assert.IsTrue(m.timeline&&m.sets.Length>0&&m.sets.All(x=>x));
        foreach(var m in bdsp.moves)Assert.IsNotNull(m.sequence);
    }
}
