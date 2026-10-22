using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BattleLab.Native
{
    public enum ZaSlot { Strike, Rush, Ranged, Stream, Roar, Direction }
    // One move's timeline as Z-A runs it (decompiled hold handlers, 30 fps): the charge clip from frame 0,
    // the attack clip at its frame, the bullet at the shot frame, then the loop hold and the end clip.
    public static class ZaTimeline
    {
        public const float Fps = 30;
        // Where the impact sits in a Z-A attack clip (20 frames); shorter clips from other games land 60% in (tunable).
        public const float ImpactFrames = 20, ShortClipImpact = .6f;

        public const float HoldCap = 30; // AwaitableSecond on the hold commands
        public static float ChargeHold(ZaMove m) => Mathf.Min(HoldCap, Mathf.Max(0, (m.chargeFrames - 10) / Fps));
        public static float ImpactHold(ZaMove m) => m.impactSpeed > 0 ? Mathf.Max(0, (int)(20f / m.impactSpeed - 10) / Fps) : 0;
        public static float LoopHold(ZaMove m) => Mathf.Min(HoldCap, Mathf.Max(0, m.loopFrames / Fps));
        // Fly, Dig and boss patterns: invincible and not lockable over these seconds of the move.
        public static bool Invincible(ZaMove m,float elapsed)
        {
            if(!string.IsNullOrEmpty(m.tlInvWindows))
            {
                foreach(var window in m.tlInvWindows.Split(','))
                {
                    var ends=window.Split(':');
                    if(ends.Length==2 && int.TryParse(ends[0],out int a) && int.TryParse(ends[1],out int b) && InWindow(m,elapsed,a,b))return true;
                }
                return false;
            }
            return m.tlInvStart>=0 && m.tlInvEnd>m.tlInvStart && InWindow(m,elapsed,m.tlInvStart,m.tlInvEnd);
        }
        static bool InWindow(ZaMove m,float elapsed,int from,int to)=>elapsed>=Seconds(m,from) && elapsed<Seconds(m,to);
        // Seconds from the move's start until a timeline frame, counting every hold passed before it.
        public static float Seconds(ZaMove m, int frame)
        {
            float t = frame / Fps;
            foreach(var h in Holds(m))if(h.frame>=0 && h.frame<frame)
                t+=h.kind==0?ChargeHold(m):h.kind==1?ImpactHold(m):LoopHold(m);
            return t;
        }
        static (int frame,int kind)[] Holds(ZaMove m)
        {
            if(m.holds!=null)return m.holds;
            if(m.tlHolds==null)return m.holds=new[]{(m.tlCharge,0),(m.tlImpact,1),(m.tlLoop,2)};
            var list=new List<(int,int)>();
            foreach(var s in m.tlHolds.Split(','))
            {var p=s.Split(':');if(p.Length==2 && int.TryParse(p[0],out int f) && int.TryParse(p[1],out int k))list.Add((f,k));}
            return m.holds=list.Distinct().ToArray();
        }
        public static float ShotAt(ZaMove m,int index)
        {
            if(m.shotSchedule==null)
            {
                var list=new List<float>();
                foreach(var s in (m.shotTimes??"").Split(','))
                    if(float.TryParse(s,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float t))list.Add(t);
                m.shotSchedule=list.ToArray();
            }
            if(m.shotSchedule.Length==0)return ShootAt(m);
            int last=m.shotSchedule.Length-1;
            return index<=last?m.shotSchedule[Mathf.Max(0,index)]:m.shotSchedule[last]+(index-last)*(last>0?m.shotSchedule[last]-m.shotSchedule[last-1]:0);
        }
        public static float AttackAt(ZaMove m) => m.tlAttack >= 0 ? Mathf.Min(Seconds(m, m.tlAttack), ShootAt(m)) : 0;
        public static float ShootAt(ZaMove m) => Seconds(m, m.tlShoot);
        public static float LockUntil(ZaMove m) => m.tlLock >= 0 ? Seconds(m, m.tlLock) : ShootAt(m);
        public static float LoopEnds(ZaMove m) => m.tlLoop >= 0 && m.tlLoop >= m.tlShoot ? Seconds(m, m.tlLoop + 1) : ShootAt(m);
        public static float EndsAt(ZaMove m) => Mathf.Max(Seconds(m, m.tlEnd), ShootAt(m));
        public static bool HasLoopHold(ZaMove m) => m.tlLoop >= 0 && m.tlLoop >= m.tlShoot;
        // When the end clip starts: after the loop hold, or (no hold after the shot, as in Draco Meteor's long
        // pattern) late enough that the loop runs on and the end clip finishes with the timeline.
        public static float EndClipAt(ZaMove m, float endLength) => HasLoopHold(m) ? LoopEnds(m) : Mathf.Max(ShootAt(m), EndsAt(m) - Mathf.Max(0, endLength));

        public static ZaSlot Slot(string animation, string category)
        {
            string a = animation ?? "";
            if (a.Contains("dirattack")) return ZaSlot.Direction;
            if (a.Contains("rangeattack02")) return ZaSlot.Stream;
            if (a.Contains("attack03")) return ZaSlot.Rush;
            if (a.Contains("rangeattack01")) return ZaSlot.Ranged;
            if (a.Contains("attack01")) return ZaSlot.Strike;
            if (a.Contains("roar01")) return ZaSlot.Roar;
            return category == "Special" ? ZaSlot.Ranged : category == "Status" ? ZaSlot.Roar : ZaSlot.Strike;
        }
        // start (attack phase), loop (the loop hold) and end (recovery); missing clips fall back to the nearest the model has.
        public static (AnimationClip start, AnimationClip loop, AnimationClip end) Clips(ModelEntry e, ZaSlot slot)
        {
            AnimationClip Any(params AnimationClip[] c) { foreach (var x in c) if (x) return x; return null; }
            var hit = Any(e.attack, e.special, e.notice, e.idle);
            switch (slot)
            {
                case ZaSlot.Rush: { var s = Any(e.attackStart, e.attack03, e.attack, hit); return (s, Loop(e.attackLoop), Any(e.attackEnd)); }
                case ZaSlot.Stream: { var s = Any(e.rangeStart, e.special, e.attack, hit); return (s, Loop(e.rangeLoop), Any(e.rangeEnd)); }
                case ZaSlot.Direction:
                    return e.bossAttack ? (e.bossAttack, Loop(e.bossLoop), Any(e.bossEnd)) : Clips(e, ZaSlot.Rush);
                case ZaSlot.Ranged: return (Any(e.special, e.rangeStart, e.attack, hit), null, null);
                case ZaSlot.Roar: return (Any(e.roar, e.notice, e.special, hit), null, null);
                default: return (Any(e.attack, e.attack03, e.attackStart, hit), null, null);
            }
        }
        static AnimationClip Loop(AnimationClip clip) => ClipPlayback.Loops(clip) ? clip : null;
        // PlayVirtualAnimation's UseToImpactFrame reads WazaParam +0xc4 (9ac40c); no guessed clip-length scaling.
        public static float NativeSpeed(ZaMove m, int frame) =>
            ("," + (m.tlImpactPlays ?? "") + ",").Contains("," + frame + ",") && m.impactSpeed > 0 ? m.impactSpeed : 1;

        // A stagger: stun01 start, the loop held until the stun ends, then stun01 end.
        public static (AnimationClip start, AnimationClip loop, AnimationClip end) StunClips(ModelEntry e)
        {
            var loop = ClipPlayback.Stun(e);
            return (e.stunStart ? e.stunStart : loop, e.stunStart ? loop : null, e.stunEnd);
        }
        public static AnimationClip ChargeClip(ModelEntry e) => e.charge ? e.charge : e.battle ? e.battle : e.idle;
        public static AnimationClip Idle(ModelEntry e) => e.battle ? e.battle : e.idle;

        // A body is back to idle or footwork within this of the timeline's end; a move still alive this long past
        // its last possible hit is cut; a finished one-shot never holds its last frame longer than the tolerance.
        public const float SettleMargin = 1, FailsafeMargin = 2, GapTolerance = .1f, LongestFlight = 8;
        public static float Deadline(ZaMove m) => Mathf.Max(EndsAt(m), ShotAt(m,Mathf.Max(m.shotsMax,m.plusShotsMax)-1) + LongestFlight) + FailsafeMargin;

        // The staged pattern plays ("frame:va,..."), parsed once.
        public static (int frame, int va)[] Plays(ZaMove m)
        {
            if (m.plays != null) return m.plays;
            var list = new List<(int, int)>();
            foreach (var p in (m.tlPlays ?? "").Split(','))
            {
                var kv = p.Split(':');
                if (kv.Length == 2 && int.TryParse(kv[0], out int f) && int.TryParse(kv[1], out int va)) list.Add((f, va));
            }
            return m.plays = list.ToArray();
        }
        // The clip a pattern play starts (and the loop it runs into), by Z-A virtual animation id.
        public static (AnimationClip clip, AnimationClip then) PlayClip(ModelEntry e, int va)
        {
            AnimationClip Any(params AnimationClip[] c) { foreach (var x in c) if (x) return x; return null; }
            if (va >= 54 && va <= 59)
            {
                int number = va <= 56 ? va - 53 : va - 56;
                string stem = "directionattack" + number.ToString("00");
                AnimationClip Named(string n) => e.directionAttacks?.FirstOrDefault(c => c && c.name == n);
                if (va <= 56) return (Any(Named(stem), e.bossAttack, e.attack), null);
                var loop = Loop(Named(stem + "_loop"));
                var start = Named(stem + "_start");
                if(start || loop)return (Any(start,loop),loop);
                // Normal Rayquaza has no Mega directionattack rig. Keep its own rush
                // start/loop through the dive instead of a one-shot followed by idle.
                var rush=Clips(e,ZaSlot.Rush);return (rush.start,rush.loop);
            }
            AnimationClip Aerial(string name)=>e.aerialClips?.FirstOrDefault(c=>c && c.name==name);
            // VA67/69 -> real61/63: aerial=1, then land_int=1. VA742 -> real72 mega charge.
            // VA53 -> real46 down01, a recovery/stagger pose distinct from the normal stun.
            if(va==67)return (Any(Aerial("jumpup01_start"),e.attackStart,e.attack),Loop(Aerial("jumpup01_loop")));
            if(va==68)return (Any(Aerial("jumpdown01_start"),Aerial("jumpup01_loop"),e.attackStart),Loop(Aerial("jumpdown01_loop")));
            if(va==69)return (Any(Aerial("land02"),Aerial("land01"),e.attackEnd,Idle(e)),null);
            if(va==742)return (ChargeClip(e),null);
            if(va==53)return (Any(Aerial("down01_start"),e.stunStart,e.faint),Loop(Aerial("down01_loop")));
            // VA94's real-animation row has no clip transitions; keep the current pose.
            switch (va)
            {
                case 13: return (Any(e.walk,e.run,Idle(e)),null);
                case 48: case 49: return (Any(e.damage, Idle(e)), null);
                case 52: { var c = StunClips(e); return (c.start, c.loop); }
                case 98: return (Any(e.stunEnd, Idle(e)), null);
                case 81: return (ChargeClip(e), null);
                // Recovered real animation rows 59/60, 188/204 and 86.
                case 65: case 558: return (Any(e.stepIn,e.walk,Idle(e)),null);
                case 66: case 570: return (Any(e.stepOut,e.walk,Idle(e)),null);
                case 108: return (Any(e.mega,e.roar,Idle(e)),null);
                case 47: return (Any(e.rangeStart, e.special, e.attack), Loop(e.rangeLoop));
                case 46: return (Any(e.attackStart, e.attack03, e.attack), Loop(e.attackLoop));
                case 97: return (Any(e.rangeEnd, Idle(e)), null);
                case 96: return (Any(e.attackEnd, Idle(e)), null);
                case 37: case 42: case 43: case 44: return (Any(e.attack, e.special), null);
                case 38: case 41: case 45: return Split(e, Any(e.attack03, e.attack));
                case 39: return Split(e, Any(e.special, e.attack));
                case 40: return (Any(e.roar, e.notice), null);
                default: return (null, null);
            }
        }
        static (AnimationClip clip,AnimationClip then) Split(ModelEntry e,AnimationClip clip)
        { return (clip,clip && clip.name.EndsWith("_start")?e.Named(clip.name.Replace("_start","_loop")):null); }
        public static (int frame,float rate)[] Rates(ZaMove m)
        {
            if(m.rates!=null)return m.rates;
            var result=new List<(int,float)>();
            foreach(var p in (m.tlRates??"").Split(','))
            {
                var kv=p.Split(':');
                if(kv.Length==2 && int.TryParse(kv[0],out int f) && float.TryParse(kv[1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float rate))
                    result.Add((f,Mathf.Max(0,rate)));
            }
            return m.rates=result.OrderBy(p=>p.Item1).ToArray();
        }
        public static float MasterSpeed(ZaMove m,float elapsed)
        {float rate=1;foreach(var p in Rates(m)){if(Seconds(m,p.frame)>elapsed)break;rate=p.rate;}return rate;}
        // Integrate clip time across native speed changes, including a temporary pause.
        public static float ClipEnd(ZaMove m,float at,float length,float baseSpeed)
        {
            float remaining=length,current=at,rate=MasterSpeed(m,at)*baseSpeed;
            foreach(var p in Rates(m))
            {
                float next=Seconds(m,p.frame);if(next<=at)continue;
                float consumed=(next-current)*rate;
                if(rate>0 && consumed>=remaining)return current+remaining/rate;
                remaining-=consumed;current=next;rate=p.rate*baseSpeed;
            }
            return rate>0?current+remaining/rate:Mathf.Max(current,EndsAt(m));
        }
        static float Finish((float at, AnimationClip clip, float speed) s) => s.at + (s.clip ? s.clip.length / Mathf.Max(.01f, s.speed) : 0);

        // The body's clips over a move, in seconds from its start: charge, attack, loop and end, the pattern's replays
        // between its shots, and the idle wherever a one-shot finishes early and at the end. Returns when the body is free.
        public static float Plan(ZaMove m, ModelEntry e, string category, List<(float at, AnimationClip clip, float speed)> steps)
        {
            steps.Clear();
            var (start, loop, end) = Clips(e, Slot(m.animation, category));
            float attack = AttackAt(m), shoot = ShootAt(m), ends = EndsAt(m);
            var events = new List<(float at, AnimationClip clip, float speed, AnimationClip then)>();
            if (attack > .05f) events.Add((0, ChargeClip(e), 1, null));
            bool native = m.source != null && m.source.StartsWith("za-");
            if (native && Plays(m).Length > 0)
                return NativePlan(m, e, steps);
            float rate = native ? NativeSpeed(m, m.tlAttack) : ClipSpeed(start, shoot - attack, loop != null);
            bool explicitDirection = native && Plays(m).Any(p => p.frame == m.tlAttack && PlayClip(e,p.va).clip);
            if (explicitDirection)
            {
                var first = Plays(m).First(p => p.frame == m.tlAttack && PlayClip(e,p.va).clip);
                var c = PlayClip(e, first.va);events.Add((attack, c.clip, NativeSpeed(m, first.frame), c.then));
            }
            else
            {
                events.Add((attack, start, rate, null));
                if (loop) events.Add((shoot, loop, 1, null));
            }
            var replays = new List<(float at, AnimationClip clip, float speed, AnimationClip then)>();
            foreach (var (frame, va) in Plays(m))
            {
                if (frame <= m.tlAttack || frame > m.tlEnd) continue;
                var (clip, then) = PlayClip(e, va);
                if (clip) replays.Add((Seconds(m, frame), clip, native ? NativeSpeed(m, frame) : 1, then));
            }
            if (end && !explicitDirection)
            {
                float endAt = Mathf.Max(shoot, EndClipAt(m, loop ? end.length : 0));
                if (!replays.Any(r => r.at >= endAt - .05f)) events.Add((endAt, end, 1, null));
            }
            events.AddRange(replays);
            var ordered = events.OrderBy(x => x.at).ToList();
            for (int i = 0; i < ordered.Count; i++)
            {
                var x = ordered[i]; steps.Add((x.at, x.clip, x.speed));
                if (!x.then || !x.clip) continue;
                float t = x.at + x.clip.length / Mathf.Max(.01f, x.speed);
                if (i + 1 >= ordered.Count || ordered[i + 1].at > t + .001f) steps.Add((t, x.then, 1));
            }
            var idle = Idle(e);
            float settle = ends;
            if (steps.Count > 0 && steps[steps.Count - 1].clip && !ClipPlayback.Loops(steps[steps.Count - 1].clip)) settle = Mathf.Max(settle, Finish(steps[steps.Count - 1]));
            settle = Mathf.Min(settle, ends + SettleMargin);
            steps.RemoveAll(s => s.at > settle + .001f);
            for (int i = 0; i < steps.Count; i++)
            {
                var s = steps[i];
                if (!s.clip || s.clip == idle || ClipPlayback.Loops(s.clip)) continue;
                float done = Finish(s), next = i + 1 < steps.Count ? steps[i + 1].at : settle;
                if (next - done > GapTolerance) steps.Insert(i + 1, (done, idle, 1));
            }
            if (steps.Count == 0 || steps[steps.Count - 1].clip != idle) steps.Add((settle, idle, 1));
            return settle;
        }
        // Preserve ALL native cues, including repeated charge/guard cues before the
        // first attack. A no-op VA (94) must not become a generic roar or truncate a loop.
        static float NativePlan(ZaMove m, ModelEntry e, List<(float at, AnimationClip clip, float speed)> steps)
        {
            steps.Clear();
            var commands=new List<(float at,int frame,(AnimationClip clip,AnimationClip then) c)>();
            foreach(var p in Plays(m).OrderBy(p=>p.frame))
            {
                if(p.frame>m.tlEnd)continue;
                var c=PlayClip(e,p.va);
                // VA107 clears loop_action_bool. Release the active split action
                // at this command's frame, rather than guessing from timeline length.
                if(p.va==107 && commands.Count>0)
                {
                    var prior=commands[commands.Count-1].c;
                    var end=prior.then==e.rangeLoop && e.rangeLoop?e.rangeEnd:
                        prior.then==e.attackLoop && e.attackLoop?e.attackEnd:
                        prior.then==e.bossLoop && e.bossLoop?e.bossEnd:null;
                    c=(end,null);
                }
                if(c.clip)commands.Add((Seconds(m,p.frame),p.frame,c));
            }
            var cues=commands.ToArray();
            float ends = EndsAt(m);
            for (int i=0;i<cues.Length;i++)
            {
                var x=cues[i];float speed=NativeSpeed(m,x.frame);
                steps.Add((x.at,x.c.clip,speed));
                float finish=ClipEnd(m,x.at,x.c.clip.length,Mathf.Max(.01f,speed));
                float next=i+1<cues.Length?cues[i+1].at:ends;
                if(finish+GapTolerance<next)
                {
                    if(x.c.then)steps.Add((finish,x.c.then,1));
                    else if(!ClipPlayback.Loops(x.c.clip))steps.Add((finish,Idle(e),1));
                }
            }
            // Loop-hold completion releases a split attack's native end segment.
            // Explicit later animation cues (e.g. recoil/stun) take precedence.
            if(cues.Length>0 && cues[cues.Length-1].c.then)
            {
                var last=cues[cues.Length-1];
                var end=last.c.then==e.rangeLoop?e.rangeEnd:last.c.then==e.attackLoop?e.attackEnd:e.Named(last.c.then.name.Replace("_loop","_end"));
                if(end)
                {
                    float at=EndClipAt(m,end.length);
                    if(at>last.at)steps.Add((at,end,1));
                }
            }
            steps.Sort((a,b)=>a.at.CompareTo(b.at));
            float settle=ends;
            if(steps.Count>0 && steps[steps.Count-1].clip && !ClipPlayback.Loops(steps[steps.Count-1].clip))
                settle=Mathf.Min(ends+SettleMargin,Mathf.Max(ends,ClipEnd(m,steps[steps.Count-1].at,steps[steps.Count-1].clip.length,steps[steps.Count-1].speed)));
            steps.Add((settle,Idle(e),1));return settle;
        }
        // Playback speed that lands the clip's impact (or the end of a start clip) on the shot.
        public static float ClipSpeed(AnimationClip clip, float seconds, bool startClip)
        {
            if (!clip || clip.length <= 0) return 1;
            float impact = startClip ? clip.length : Mathf.Min(ImpactFrames / Fps, clip.length * ShortClipImpact);
            return seconds <= .02f ? 2.5f : Mathf.Clamp(impact / seconds, .5f, 2.5f);
        }
    }
}
