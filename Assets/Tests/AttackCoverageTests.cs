using System.IO;
using System.Linq;
using BattleLab;
using BattleLab.Native;
using NUnit.Framework;
using UnityEngine;
public class AttackCoverageTests
{
    [Test] public void BuiltCatalogMatchesCanonicalSourceAndEveryMoveHasPlaybackAndSound()
    {
        var cat=Resources.Load<LabCatalog>("LabCatalog");var source=JsonUtility.FromJson<LabData>(File.ReadAllText("Tools/seed-data.json"));
        CollectionAssert.AreEquivalent(source.moves.Select(m=>m.id),cat.data.moves.Select(m=>m.id));
        Assert.AreEqual(cat.data.moves.Length,cat.data.moves.Select(m=>m.id).Distinct().Count());
        var effects=Resources.Load<VioletFxCatalog>("NativeZA");var timing=Resources.Load<NativeTiming>("NativeTiming");var audio=Resources.Load<LabAudio>("LabAudio");
        foreach(var move in cat.data.moves)
        {
            Assert.IsTrue(effects.Has(move.id)||Resources.Load<BdspFxCatalog>("NativeBDSP").Has(move.id),move.id);Assert.IsNotNull(timing.Get(move.id),move.id);Assert.IsTrue(audio.MoveClip(move.id),move.id);
            Assert.Greater(move.power,0,move.id);Assert.Greater(move.zaPower,0,move.id);Assert.Greater(move.cooldown,0,move.id);
            Assert.IsTrue(float.IsFinite(move.windup)&&float.IsFinite(move.recovery),move.id);
        }
        foreach(var species in cat.data.species)foreach(var id in species.moves)Assert.IsNotNull(cat.Move(id),species.id+" / "+id);
    }
    [Test] public void CombinedMultiHitDamagePreservesImmunityAndPerHitDefenseCalculation()
    {
        var cat=Resources.Load<LabCatalog>("LabCatalog");var move=JsonUtility.FromJson<MoveData>(JsonUtility.ToJson(cat.Move("TACKLE")));
        var a=cat.Species("PIKACHU");var b=cat.Species("MUDKIP");int one=CombatRules.Damage(a,b,move,cat.data.types,BattleMode.ZA,MoveStyle.Normal);
        move.hitCount=2;Assert.AreEqual(one*2,CombatRules.Damage(a,b,move,cat.data.types,BattleMode.ZA,MoveStyle.Normal));
        Assert.AreEqual(0,CombatRules.Damage(a,cat.Species("GENGAR"),move,cat.data.types,BattleMode.ZA,MoveStyle.Normal));
    }
}
