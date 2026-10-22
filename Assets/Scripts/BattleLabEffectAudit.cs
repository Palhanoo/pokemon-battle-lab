using System.Linq;
using UnityEngine;
namespace BattleLab
{
    public partial class BattleLabApp
    {
        bool effectAudit;Vector2 effectScroll;
        void EffectAuditUI()
        {
            Panel(820,190,600,440);Text(840,202,565,"ATTACK LAYERS · F2 close · Esc freeze",label);
            var effects=(fighters??System.Array.Empty<Fighter>()).Where(f=>f.action?.presentation?.Alive==true).Select(f=>f.action.presentation)
                .Concat(tails.Where(t=>t.Alive)).Distinct().ToArray();
            var renderers=effects.SelectMany(e=>e.Effect.gameObject.GetComponentsInChildren<ParticleSystemRenderer>(true)).ToArray();
            effectScroll=GUI.BeginScrollView(new Rect(838,245,562,355),effectScroll,new Rect(0,0,535,renderers.Length*76+35));
            for(int i=0;i<renderers.Length;i++)
            {
                var r=renderers[i];var m=r.sharedMaterial;var ps=r.GetComponent<ParticleSystem>();
                r.enabled=GUI.Toggle(new Rect(0,i*76,520,27),r.enabled,r.name+" · "+ps.particleCount+" particles");
                var textures=new[]{"_MainTex","_SubTex","_ThirdTex"}.Where(p=>m.HasProperty(p)&&m.GetTexture(p)).Select(p=>m.GetTexture(p).name);
                Text(20,i*76+29,510,string.Join(" + ",textures),small,44);
            }
            if(renderers.Length==0)Text(0,0,530,"Use an attack, then Esc to inspect its layers.",small);
            GUI.EndScrollView();Text(840,604,560,"Toggle a layer to isolate it. Reset restores every layer.",muted);
        }
    }
}
