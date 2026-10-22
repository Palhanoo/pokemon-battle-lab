using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleLab.Native
{
    // Brilliant Diamond move effects for the Gen-3 moves Violet has none for: move id -> exported sequence,
    // the effect prefabs it creates, their textures and the shared VioletParticle material. Built by
    // BdspFxImporter from Assets/Generated/MoveFx/bdsp.
    public class BdspFxCatalog : ScriptableObject
    {
        [Serializable]
        public class Move
        {
            public string id;
            public TextAsset sequence;
        }

        [Serializable]
        public class Locator
        {
            public int node;
            public string name;
        }

        public Material material;
        public List<Move> moves = new();
        public List<TextAsset> prefabs = new();
        public List<Texture2D> textures = new();
        public List<Locator> locators = new();

        Dictionary<string, Move> byId;
        Dictionary<string, TextAsset> prefabByName;
        Dictionary<string, Texture2D> texByName;
        Dictionary<int, string> locatorNames;

        public bool Has(string moveId) => Find(moveId) != null;

        Move Find(string moveId)
        {
            if (byId == null)
            {
                byId = new Dictionary<string, Move>();
                foreach (var m in moves) byId[m.id] = m;
                prefabByName = new Dictionary<string, TextAsset>();
                foreach (var p in prefabs) if (p) prefabByName[p.name] = p;
                texByName = new Dictionary<string, Texture2D>();
                foreach (var t in textures) if (t) texByName[t.name] = t;
                locatorNames = new Dictionary<int, string>();
                foreach (var l in locators) locatorNames[l.node] = l.name;
            }
            if (string.IsNullOrEmpty(moveId)) return null;
            var key = moveId.StartsWith("MOVE_") ? moveId : "MOVE_" + moveId;
            return byId.TryGetValue(key, out var found) ? found : null;
        }

        // A stopped-until-its-frames BdspMove between the two battlers, or null if the move has no BDSP effect.
        public BdspMove Play(string moveId, VioletMove.Battler attacker, VioletMove.Battler defender)
        {
            var m = Find(moveId);
            if (m == null || !material || !m.sequence) return null;
            return BdspMove.Create(m.sequence.text, n => prefabByName.TryGetValue(n, out var p) ? p.text : null,
                                   n => texByName.TryGetValue(n, out var t) ? t : null, material, locatorNames, attacker, defender);
        }
    }
}
