using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleLab.Native
{
    // Scarlet/Violet move effects available in the build: move id (MOVE_EMBER) -> exported timeline and
    // emitter sets, plus the shared mask textures and the VioletParticle material. Built by MoveFxImporter.
    public class VioletFxCatalog : ScriptableObject
    {
        [Serializable]
        public class Move
        {
            public string id, folder;
            public TextAsset timeline;
            public TextAsset[] sets;
        }

        public Material material;
        public List<Move> moves = new();
        public List<Texture2D> textures = new();

        Dictionary<string, Move> byId;
        Dictionary<string, Texture2D> byName;

        public bool Has(string moveId) => Find(moveId) != null;
        // Z-A exports (za* folders) bake the move's holds into their timeline, so they run on the move's own clock from its start.
        public bool OnMoveClock(string moveId) => Find(moveId)?.folder?.StartsWith("za") == true;

        Move Find(string moveId)
        {
            if (byId == null)
            {
                byId = new Dictionary<string, Move>();
                foreach (var mv in moves) byId[mv.id] = mv;
                byName = new Dictionary<string, Texture2D>();
                foreach (var t in textures) if (t) byName[t.name] = t;
            }
            if (string.IsNullOrEmpty(moveId)) return null;
            var key = moveId.StartsWith("MOVE_") ? moveId : "MOVE_" + moveId;
            return byId.TryGetValue(key, out var found) ? found : null;
        }

        // A stopped-until-its-frames VioletMove between the two battlers, or null if the move has no Violet effect.
        public VioletMove Play(string moveId, VioletMove.Battler attacker, VioletMove.Battler defender, Func<string, bool> keep = null, int shotCount = -1)
        {
            var m = Find(moveId);
            if (m == null || !material) return null;
            return VioletMove.Create(m.timeline.text, SetJson(m), n => byName.TryGetValue(n, out var t) ? t : throw new InvalidOperationException("Missing ZA effect texture: "+n), material, attacker, defender, keep, shotCount);
        }

        static Func<string, string> SetJson(Move m) => file =>
        {
            foreach (var s in m.sets) if (s && s.name + ".json" == file) return s.text;
            return null;
        };
    }
}
