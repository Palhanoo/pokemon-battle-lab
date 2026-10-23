using System.Linq;
using BattleLab;
using BattleLab.Native;
using NUnit.Framework;
using UnityEngine;
public class RosterAndEffectsTests
{
    [Test] public void MixedSourcesHaveSixChoicesWithDataAndSound()
    {
        var cat=Resources.Load<LabCatalog>("LabCatalog");var audio=Resources.Load<LabAudio>("LabAudio");
        Assert.AreEqual(9,cat.data.species.Length);Assert.AreEqual(15,cat.models.Length);Assert.AreEqual(53,cat.data.moves.Length);
        foreach(var species in cat.data.species)
        {
            Assert.AreEqual(6,species.moves.Distinct().Count(),species.id);
            Assert.IsTrue(cat.models.Any(m=>m.species==species.id),species.id);
            foreach(var move in species.moves){Assert.IsNotNull(cat.Move(move));Assert.IsTrue(audio.MoveClip(move),move);}
        }
        Assert.IsTrue(cat.models.Any(m=>m.species=="POOCHYENA"&&m.source=="SV"));
        Assert.IsTrue(cat.models.Any(m=>m.species=="BRELOOM"&&m.source=="BDSP"));
    }
    [Test] public void AuraSphereUsesItsThirdSlotMaskAndXZPrimitiveBasis()
    {
        var cat=Resources.Load<VioletFxCatalog>("NativeZA");
        var move=cat.moves.Single(m=>m.id=="MOVE_AURA_SPHERE");var set=move.sets.Single(s=>s.name=="ew0396_bullet");
        var root=VioletFx.Build(set.text,n=>cat.textures.FirstOrDefault(t=>t.name==n),cat.material);
        try
        {
            var ps=root.GetComponentsInChildren<ParticleSystem>().Single(p=>p.name=="allowhead_02");
            var material=ps.GetComponent<ParticleSystemRenderer>().sharedMaterial;
            Assert.AreEqual("cpt_3_blur0202s",material.GetTexture("_MainTex").name,"slot-2-only layer must not be a solid white mask");
            Assert.AreEqual(Mathf.PI/2,material.GetFloat("_MainRotation"),.001f);
            Assert.AreEqual(-Mathf.PI/2,ps.main.startRotationX.constantMax,.001f,"orient the long native primitive into the projectile plane, without shrinking its dimensions");
        }
        finally
        {
            foreach(var r in root.GetComponentsInChildren<ParticleSystemRenderer>())Object.DestroyImmediate(r.sharedMaterial);
            Object.DestroyImmediate(root);
        }
    }
    [Test] public void ImportedSoundsContainAudioSamples()
    {
        var cat=Resources.Load<LabAudio>("LabAudio");
        Assert.AreEqual(53,cat.moves.Length);Assert.AreEqual(8,cat.cries.Length);
        foreach(var clip in cat.moves.Select(m=>m.clip).Concat(cat.cries.Select(c=>c.clip)).Distinct())
        {
            Assert.Greater(clip.samples,100);Assert.IsTrue(clip.LoadAudioData());
            var samples=new float[clip.samples*clip.channels];Assert.IsTrue(clip.GetData(samples,0));
            Assert.IsTrue(samples.Any(s=>Mathf.Abs(s)>.0001f),clip.name+" is silent");
        }
    }
}
