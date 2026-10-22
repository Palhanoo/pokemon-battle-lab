using UnityEngine;
namespace BattleLab
{
    public static class ClashRules
    {
        // Damage already includes the attacker's STAB and its intended target's defenses/types.
        public static (int a,int b) Resolve(int a,int b)
        {
            if(a<=0&&b<=0)return (0,0);
            if(a>b&&a>=1.5f*b)return (a-b,0);
            if(b>a&&b>=1.5f*a)return (0,b-a);
            return (0,0);
        }
        public static bool SweptContact(Vector3 a0,Vector3 a1,Vector3 b0,Vector3 b1,float radius,out Vector3 point)
        {
            var relative=a0-b0;var delta=(a1-a0)-(b1-b0);
            float t=delta.sqrMagnitude<1e-8f?0:Mathf.Clamp01(-Vector3.Dot(relative,delta)/delta.sqrMagnitude);
            var a=Vector3.Lerp(a0,a1,t);var b=Vector3.Lerp(b0,b1,t);point=(a+b)*.5f;
            return (a-b).sqrMagnitude<=radius*radius;
        }
    }
}
