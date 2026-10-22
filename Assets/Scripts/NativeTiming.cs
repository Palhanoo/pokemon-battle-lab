using System;
using BattleLab.Native;
using UnityEngine;
namespace BattleLab
{
    public class NativeTiming : ScriptableObject
    {
        [Serializable] public class Row { public string id; public ZaMove move; }
        [Serializable] public class Data { public Row[] rows; }
        public Row[] rows;
        public ZaMove Get(string id) => Array.Find(rows,r=>r.id==id)?.move;
    }
}
