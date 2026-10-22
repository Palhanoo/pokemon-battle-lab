using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BattleLab.Tests
{
    public class CombatRulesTests
    {
        [Test] public void PriorityWinsOverSpeed()=>Assert.AreEqual(0,CombatRules.First(20,1,150,0,new System.Random(42)));
        [Test] public void SpeedOrdersEqualPriority()=>Assert.AreEqual(1,CombatRules.First(90,0,110,0,new System.Random(42)));
        [Test] public void AgileCanEarnConsecutiveActions()
        {
            var normal=CombatRules.Preview(0,145,100,100,MoveStyle.Normal,5);
            var agile=CombatRules.Preview(0,145,100,100,MoveStyle.Agile,5);
            Assert.AreEqual(1,normal[2]);Assert.AreEqual(0,agile[2]);
        }
        [Test] public void StrongDelaysFollowingAction()
        {Assert.Greater(CombatRules.ActionDelay(90,MoveStyle.Strong),CombatRules.ActionDelay(90,MoveStyle.Normal));Assert.AreEqual(2,CombatRules.StyleCost(MoveStyle.Strong));}
        [Test] public void TypeImmunityRemainsZeroInAllModes()
        {
            var chart=new[]{new TypePair{attack="NORMAL",defend="GHOST",factor=0}};
            foreach(BattleMode mode in Enum.GetValues(typeof(BattleMode)))Assert.AreEqual(0,CombatRules.Effectiveness(chart,"NORMAL",new[]{"GHOST"},mode));
        }
        [Test] public void ZaUsesItsResistedMultiplier()
        {
            var chart=new[]{new TypePair{attack="STEEL",defend="STEEL",factor=.5f}};
            Assert.AreEqual(.6f,CombatRules.Effectiveness(chart,"STEEL",new[]{"STEEL"},BattleMode.ZA));
            Assert.AreEqual(.5f,CombatRules.Effectiveness(chart,"STEEL",new[]{"STEEL"},BattleMode.BDSP));
        }
        [Test] public void SourceSwitchDoesNotChangeCombatData()
        {
            var c=Resources.Load<LabCatalog>("LabCatalog");Assert.NotNull(c);Assert.AreEqual(9,c.models.Length);
            foreach(var s in c.data.species)
            {foreach(var source in new[]{"Arceus","ZA","BDSP"}){var m=c.Model(s.id,source);Assert.NotNull(m.prefab);Assert.NotNull(m.Role("attack"));Assert.NotNull(m.Role("damage"));}}
        }
        [Test] public void SelectedRealtimeRowsAreNativeAndUsable()
        {
            var c=Resources.Load<LabCatalog>("LabCatalog");
            foreach(var m in c.data.moves){Assert.AreEqual("za-v2",m.source,m.id);Assert.Greater(m.cooldown,0);Assert.Greater(m.rangeMax,0);}
            Assert.AreEqual(3,c.Move("QUICK_ATTACK").cooldown);Assert.AreEqual(8,c.Move("THUNDERBOLT").cooldown);
        }
    }
}
