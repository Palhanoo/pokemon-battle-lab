using NUnit.Framework;
using UnityEngine;
using BattleLab;
public class ImmersionTests
{
    [Test] public void AimRequiresFiniteBodyContact()
    {
        var body=new Bounds(new Vector3(0,1,5),Vector3.one);
        Assert.IsTrue(AimRules.Contact(new Vector3(0,1,0),new Vector3(0,1,8),body,.1f,out float distance));Assert.That(distance,Is.InRange(4.3f,4.5f));
        Assert.IsFalse(AimRules.Contact(new Vector3(0,1,0),new Vector3(0,1,3),body,.1f,out _));
        Assert.IsFalse(AimRules.Contact(new Vector3(2,1,0),new Vector3(2,1,8),body,.1f,out _));
        Assert.IsTrue(AimRules.Contact(body.center,body.center,body,.1f,out _));
        Assert.IsFalse(AimRules.Contact(Vector3.zero,Vector3.zero,body,.1f,out _));
    }
    [Test] public void FlightEscapesGroundLevelButNotAimedShots()
    {
        var flying=new Bounds(new Vector3(0,3,5),Vector3.one);
        Assert.IsFalse(AimRules.Contact(new Vector3(0,.5f,0),new Vector3(0,.5f,8),flying,.2f,out _));
        Assert.IsTrue(AimRules.Contact(new Vector3(0,1,0),flying.center,flying,.2f,out _));
    }
    [Test] public void WaterBoostRequiresWaterSpeciesInsideLaneAndGrounded()
    {
        var water=new Vector3(-6,0,6);
        Assert.AreEqual(1.65f,AimRules.WaterSpeed(true,water,0));
        Assert.AreEqual(1,AimRules.WaterSpeed(false,water,0));
        Assert.AreEqual(1,AimRules.WaterSpeed(true,Vector3.zero,0));
        Assert.AreEqual(1,AimRules.WaterSpeed(true,water,2));
    }
}
