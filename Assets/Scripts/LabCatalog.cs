using System;
using System.Linq;
using UnityEngine;

namespace BattleLab
{
    public enum BattleMode { BDSP, Arceus, ZA }
    public enum EffectSource { Auto, ZA, BDSP }
    public enum MoveStyle { Normal, Agile, Strong }
    [Serializable] public class SpeciesData { public string id; public int[] stats; public string[] types, moves; }
    [Serializable] public class MoveData
    {
        public string id, name, type, shape, source, effectOrigin, labNotes;
        public int power, accuracy, pp, priority, zaPower, hitCount;
        public bool physical;
        public float cooldown, windup, recovery, rangeMin, rangeMax, radius, speed;
    }
    [Serializable] public class TypePair { public string attack, defend; public float factor; }
    [Serializable] public class LabData { public SpeciesData[] species; public MoveData[] moves; public TypePair[] types; }
    [Serializable] public class ClipRole { public string role, clip; }
    [Serializable] public class ModelEntry
    {
        public string species, source;
        public GameObject prefab;
        public AnimationClip[] clips;
        public ClipRole[] roles;
        public AnimationClip Named(string name) => clips.FirstOrDefault(c=>c.name==name);
        public AnimationClip idle => Role("idle");
        public AnimationClip walk => Role("walk");
        public AnimationClip run => Role("run");
        public AnimationClip notice => Role("notice");
        public AnimationClip roar => Role("roar");
        public AnimationClip sleep => Role("sleep");
        public AnimationClip eat => Role("eat");
        public AnimationClip fly => Role("fly");
        public AnimationClip battle => Role("battle");
        public AnimationClip attack => Role("attack");
        public AnimationClip special => Role("special");
        public AnimationClip damage => Role("damage");
        public AnimationClip faint => Role("faint");
        public AnimationClip attack03 => Role("attack03");
        public AnimationClip attackStart => Role("attackStart");
        public AnimationClip attackLoop => Role("attackLoop");
        public AnimationClip attackEnd => Role("attackEnd");
        public AnimationClip rangeStart => Role("rangeStart");
        public AnimationClip rangeLoop => Role("rangeLoop");
        public AnimationClip rangeEnd => Role("rangeEnd");
        public AnimationClip charge => Role("charge");
        public AnimationClip stun => Role("stun");
        public AnimationClip mega => Role("mega");
        public AnimationClip bossAttack => Role("bossAttack");
        public AnimationClip stepIn => Role("stepIn");
        public AnimationClip stepOut => Role("stepOut");
        public AnimationClip stepLeft => Role("stepLeft");
        public AnimationClip stepRight => Role("stepRight");
        public AnimationClip stunStart => Role("stunStart");
        public AnimationClip stunEnd => Role("stunEnd");
        public AnimationClip bossLoop => Role("bossLoop");
        public AnimationClip bossEnd => Role("bossEnd");
        public AnimationClip[] aerialClips => clips;
        public AnimationClip[] directionAttacks => clips;
        public AnimationClip Role(string role) => clips.FirstOrDefault(c => c.name == roles.FirstOrDefault(r => r.role == role)?.clip);
    }
    public class LabCatalog : ScriptableObject
    {
        public LabData data;
        public ModelEntry[] models;
        public MoveData Move(string id) => data.moves.First(m => m.id == id);
        public SpeciesData Species(string id) => data.species.First(s => s.id == id);
        public ModelEntry Model(string species, string source) => models.First(m => m.species == species && m.source == source);
    }
}
