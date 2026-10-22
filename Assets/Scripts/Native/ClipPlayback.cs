using UnityEngine;
using UnityEngine.Playables;

namespace BattleLab.Native
{
    // glTFast imports every clip with loopTime on, so a one-shot (damage01, stun01_start, down01) would cycle forever.
    // Held clips are named *_loop or *wait*; anything else posed in battle or a stun plays once and holds its last frame.
    public static class ClipPlayback
    {
        public static bool Loops(AnimationClip c) => c && Loops(c.name);
        public static bool Loops(string name) => name != null && (name.Contains("loop") || name.Contains("wait"));
        public static float EndTime(AnimationClip c) => c ? Mathf.Max(0, c.length - .001f) : 0;

        // Freezes a finished one-shot on its last frame. True once it has finished.
        public static bool Hold<T>(T p, AnimationClip c) where T : struct, IPlayable
        {
            if (!p.IsValid() || !c || Loops(c)) return false;
            float end = EndTime(c);
            if (p.GetTime() < end) return false;
            if (p.GetSpeed() != 0) { p.SetTime(end); p.SetSpeed(0); }
            return true;
        }

        // Z-A's stun01 where the model has one, else a fallback reaction (held, never cycled).
        public static AnimationClip Stun(ModelEntry e, AnimationClip fallback = null) =>
            e == null ? null : e.stun ? e.stun : fallback ? fallback : e.damage ? e.damage : e.idle;
    }
}
