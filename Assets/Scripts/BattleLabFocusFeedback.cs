using System.Collections.Generic;
using UnityEngine;
namespace BattleLab
{
    public partial class BattleLabApp
    {
        readonly List<(Vector3 at,string text,float until,bool outgoing)> damageLabels=new();
        float hitConfirmUntil;string hitConfirm;
        static Vector3 ClampToArena(Fighter f,Vector3 next)
        {
            float x=Mathf.Max(f.body.FootprintRadius(Vector3.right),f.body.FootprintRadius(Vector3.left));
            float z=Mathf.Max(f.body.FootprintRadius(Vector3.forward),f.body.FootprintRadius(Vector3.back));
            next.x=Mathf.Clamp(next.x,-49+Mathf.Min(45,x),49-Mathf.Min(45,x));
            next.z=Mathf.Clamp(next.z,-49+Mathf.Min(45,z),49-Mathf.Min(45,z));return next;
        }
        void ConfirmHit(Fighter attacker,Fighter target,int damage)
        {
            string text=damage>0?"−"+damage:"IMMUNE";
            damageLabels.Add((target.body.CameraAnchor,text,now+1.1f,attacker==Player));
            if(attacker==Player){hitConfirmUntil=now+.65f;hitConfirm=damage>0?"HIT  −"+damage+" HP":"IMMUNE · no damage";}
        }
        void HealthBar(float x,float y,float width,Fighter f)
        {
            var old=GUI.color;GUI.color=new Color(.03f,.06f,.08f,.9f);GUI.DrawTexture(new Rect(x,y,width,9),Texture2D.whiteTexture);
            GUI.color=f.team==0?accent:new Color(1,.47f,.32f);GUI.DrawTexture(new Rect(x,y,width*Mathf.Clamp01((float)f.hp/f.MaxHp),9),Texture2D.whiteTexture);GUI.color=old;
        }
        void FocusFeedbackUI()
        {
            var target=Target;
            if(target!=null)
            {
                var at=cam.WorldToViewportPoint(target.body.CameraAnchor+Vector3.up*(target.body.BodyHeight*.55f+.25f));
                bool visible=at.z>0&&at.x>.09f&&at.x<.91f&&at.y>.2f&&at.y<.82f;
                float x=Mathf.Clamp(at.x*1440,150,1290),y=Mathf.Clamp((1-at.y)*900,145,620);
                if(at.z<0){x=at.x<.5f?1290:150;y=450;}
                if(Enemies>1&&x>980)y=Mathf.Max(y,230+17*Enemies);
                Text(x-130,y-32,260,(visible?"▼ FOCUS · ":"FOCUS → ")+target.data.id,small);
                HealthBar(x-70,y,140,target);
            }
            HealthBar(38,102,375,Player);if(target!=null)HealthBar(1106,102,296,target);
            for(int i=damageLabels.Count-1;i>=0;i--)
            {
                var item=damageLabels[i];if(now>=item.until){damageLabels.RemoveAt(i);continue;}
                var at=cam.WorldToScreenPoint(item.at+Vector3.up*(.5f+(1.1f-item.until+now)*.6f));if(at.z<=0)continue;
                var old=GUI.color;GUI.color=item.outgoing?new Color(1,.9f,.5f):new Color(1,.5f,.4f);
                Text(at.x*1440/Screen.width+30,(Screen.height-at.y)*900/Screen.height-25,220,item.text,title,45);GUI.color=old;
            }
            if(now<hitConfirmUntil){Panel(590,560,260,45);Text(607,568,235,hitConfirm,label);}
            Text(22,463,400,"View: "+cameraView+" · "+(immersive?"Manual aim":"Focused attacks"),small);
        }
    }
}
