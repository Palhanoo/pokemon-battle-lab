using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleLab.Native
{
    public enum ZaShape { Melee, Projectile, Beam, Arc, Area, Self }
    [Serializable] public sealed class ZaMove
    {
        public string source, animation;
        public int plusPower, shotsMin=1, shotsMax=1, plusShotsMin=1, plusShotsMax=1;
        public int trainerDamage; // explicit native SimpleDamage override, otherwise use the ordinary formula
        public int sourceId, power, bulletId, volumeType, homingType;
        // The move's Z-A timeline (30 fps frames, -1 absent) and the WazaParam values its holds read.
        public int tlCharge=9, tlAttack=10, tlImpact=19, tlShoot=20, tlLoop=21, tlEnd=32, tlLock=20, tlInvStart=-1, tlInvEnd=-1, attackVa, chargeFrames=15, loopFrames=10;
        public int moveType=1; // playWazaMoveType: 0 stay, 1 close on target, 2 by the trainer, 3 trainer then target
        public float impactSpeed=2;
        public string tlInvWindows;
        public string tlHolds,shotTimes;
        internal (int frame,int kind)[] holds;
        internal float[] shotSchedule;
        public string tlRates; // native master animation-speed commands, frame:multiplier
        internal (int frame,float rate)[] rates;
        public string tlImpactPlays; // frames whose native animation command uses impactMotionSpeed
        public string tlPlays; // a move's attacker clip plays, "frame:virtual animation id" in order
        internal (int frame,int va)[] plays;
        public float cooldown, windup, recovery, rangeMin, rangeMax, radius, height, speed, lifetime, distance, homing, arc, effectTime, megaGain, plusScale=1, plusRange, plusDuration, knockback, knockbackTime;
        public bool plus;
        public ZaShape shape;
        public float Reach(bool enhanced) => enhanced && plusRange>0 && plusRange<90 ? plusRange : rangeMax;
        public float Radius(bool enhanced) => Mathf.Max(.1f,radius)*(enhanced?Mathf.Max(1,plusScale):1);
    }
}
