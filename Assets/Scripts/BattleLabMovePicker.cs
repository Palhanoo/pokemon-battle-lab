using System;
using System.Linq;
using UnityEngine;
namespace BattleLab
{
    public partial class BattleLabApp
    {
        int movePickerSlot=-1;string moveSearch="";bool allTestMoves;Vector2 moveScroll;
        void OpenMovePicker(int slot){movePickerSlot=slot;moveSearch="";moveScroll=Vector2.zero;}
        bool TestOnlyMove(Fighter f,string move)=>pokedex?.Row(f.data.id)?.testOnlyMoves?.Contains(move)==true;
        string NativeSource(MoveData move)=>string.IsNullOrEmpty(move.effectOrigin)?"ZA":move.effectOrigin;
        void EquipMove(string id)
        {
            var f=Player;if(f==null||movePickerSlot<0)return;
            int index=Array.IndexOf(f.data.moves,id);
            if(index<0)
            {
                index=f.data.moves.Length;f.data.moves=f.data.moves.Append(id).ToArray();
                var row=pokedex?.Row(f.data.id);
                if(row!=null){row.data.moves=f.data.moves;row.testOnlyMoves=(row.testOnlyMoves??Array.Empty<string>()).Append(id).Distinct().ToArray();}
            }
            var slots=Loadout(f.data.id);int duplicate=Array.IndexOf(slots,index);
            if(duplicate>=0)slots[duplicate]=slots[movePickerSlot];
            slots[movePickerSlot]=index;movePickerSlot=-1;ResetBattle();
        }
        void MovePickerUI()
        {
            var f=Player;if(f==null){movePickerSlot=-1;return;}
            Panel(310,110,1110,750);
            Text(330,127,900,$"{f.data.id} · CHOOSE MOVE {movePickerSlot+1}",title,42);
            if(Button(new Rect(1275,130,120,35),"Close")){movePickerSlot=-1;return;}
            moveSearch=GUI.TextField(new Rect(330,184,740,35),moveSearch);
            if(Button(new Rect(1085,184,310,35),allTestMoves?"All lab moves · test overrides":"Species move pool")){allTestMoves=!allTestMoves;moveScroll=Vector2.zero;}
            var pool=catalog.data.moves.Where(m=>(allTestMoves||f.data.moves.Contains(m.id))&&(string.IsNullOrWhiteSpace(moveSearch)||(m.name+" "+m.type).IndexOf(moveSearch,StringComparison.OrdinalIgnoreCase)>=0)).OrderBy(m=>m.name).ToArray();
            Text(330,228,1060,$"{pool.Length} moves · search name or type · selecting an equipped move swaps slots",small);
            moveScroll=GUI.BeginScrollView(new Rect(330,264,1065,494),moveScroll,new Rect(0,0,1038,Mathf.CeilToInt(pool.Length/2f)*92));
            int first=Mathf.Max(0,Mathf.FloorToInt(moveScroll.y/92)-1)*2,last=Mathf.Min(pool.Length,first+16);
            for(int i=first;i<last;i++)
            {
                var m=pool[i];bool test=!f.data.moves.Contains(m.id)||TestOnlyMove(f,m.id);
                string variants=NativeSource(m)+(bdspFx.Has(m.id)&&NativeSource(m)!="BDSP"?" + BDSP":"");
                if(Button(new Rect(i%2*518,i/2*92,508,84),$"{m.name}{(test?" *":"")} · {m.type}\nPower {m.zaPower} · {m.cooldown:0.#}s cooldown · {variants}")){EquipMove(m.id);break;}
            }
            GUI.EndScrollView();
            Text(330,772,1060,"* Test override, not a known learnset choice. Four equipped moves per Pokémon.",small);
            Text(330,801,1060,"Damage-move lab: secondary/status/special rules are not complete. Multi-hit moves use one combined damage event. Missing presentations are excluded.",small,48);
        }
    }
}
