using System;
using System.Linq;

namespace BattleLab
{
    // Small, explicit comparison rules. No world, save, scene or rendering dependency.
    public static class CombatRules
    {
        public static int StyleCost(MoveStyle style) => style == MoveStyle.Normal ? 1 : 2;
        public static float StylePower(MoveStyle style) => style == MoveStyle.Agile ? .75f : style == MoveStyle.Strong ? 1.25f : 1;
        // Experimental action-delay curve, NOT the recovered Arceus formula.
        public static double ActionDelay(int speed, MoveStyle style) => 1000.0 / Math.Sqrt(Math.Max(1, speed)) *
            (style == MoveStyle.Agile ? .65 : style == MoveStyle.Strong ? 1.5 : 1);
        public static int First(int speedA, int priorityA, int speedB, int priorityB, Random rng)
            => priorityA != priorityB ? (priorityA > priorityB ? 0 : 1) : speedA != speedB ? (speedA > speedB ? 0 : 1) : rng.Next(2);
        public static bool Hits(int accuracy, Random rng) => accuracy <= 0 || rng.Next(100) < accuracy;
        public static float Effectiveness(TypePair[] chart, string attack, string[] defenders, BattleMode mode)
        {
            float result = 1;
            foreach (var type in defenders)
            {
                float f = chart.FirstOrDefault(p => p.attack == attack && p.defend == type)?.factor ?? 1;
                if (mode == BattleMode.ZA && f == .5f) f = .6f;
                result *= f;
            }
            return result;
        }
        public static int Damage(SpeciesData a, SpeciesData b, MoveData move, TypePair[] chart, BattleMode mode, MoveStyle style)
        {
            float type = Effectiveness(chart, move.type, b.types, mode);
            if (type == 0) return 0;
            int power = mode == BattleMode.ZA ? move.zaPower : move.power;
            float attack = a.stats[move.physical ? 1 : 4] + 35;
            float defense = b.stats[move.physical ? 2 : 5] + 35;
            float stab = a.types.Contains(move.type) ? 1.5f : 1;
            // Fixed level 30, no random damage/crit/secondary effects: shared lab baseline.
            return Math.Max(1,move.hitCount) * Math.Max(1, (int)((14 * power * attack / defense / 50 + 2) * stab * type *
                (mode == BattleMode.Arceus ? StylePower(style) : 1) * (mode == BattleMode.ZA ? .7f : 1)));
        }
        public static int NextActor(double a, double b) => a <= b ? 0 : 1;
        public static int[] Preview(double a, double b, int speedA, int speedB, MoveStyle playerStyle, int count)
        {
            var result = new int[count];
            for (int i=0;i<count;i++)
            {
                int actor = NextActor(a,b); result[i]=actor;
                if(actor==0) a+=ActionDelay(speedA,playerStyle); else b+=ActionDelay(speedB,MoveStyle.Normal);
            }
            return result;
        }
    }
}
