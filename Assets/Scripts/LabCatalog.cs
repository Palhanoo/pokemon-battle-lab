using System;
using System.Linq;
using UnityEngine;

namespace BattleLab
{
    public enum BattleMode { BDSP, Arceus, ZA }
    public enum MoveStyle { Normal, Agile, Strong }
    [Serializable] public class SpeciesData { public string id; public int[] stats; public string[] types, moves; }
    [Serializable] public class MoveData
    {
        public string id, name, type, shape, source;
        public int power, accuracy, pp, priority, zaPower;
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
