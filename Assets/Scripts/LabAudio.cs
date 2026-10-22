using System;
using UnityEngine;
namespace BattleLab
{
    public class LabAudio:ScriptableObject
    {
        [Serializable] public class Move {public string id,sound;public bool substitute;public AudioClip clip;}
        [Serializable] public class Cry {public string species;public AudioClip clip;}
        [Serializable] public class Index {public Move[] moves;public string[] cries;public string source;}
        public Move[] moves;public Cry[] cries;
        public AudioClip MoveClip(string id)=>Array.Find(moves,m=>m.id==id)?.clip;
        public AudioClip CryClip(string species)=>Array.Find(cries,c=>c.species==species)?.clip;
    }
}
