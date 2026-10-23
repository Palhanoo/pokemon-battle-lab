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
        // Closest points on finite segments: a beam occupies its entire visible span.
        public static bool SegmentContact(Vector3 p,Vector3 q,Vector3 r,Vector3 s,float radius,out Vector3 point)
        {
            var u=q-p;var v=s-r;var w=p-r;
            float a=Vector3.Dot(u,u),b=Vector3.Dot(u,v),c=Vector3.Dot(v,v),d=Vector3.Dot(u,w),e=Vector3.Dot(v,w);
            float x=0,y=0,den=a*c-b*b;
            if(a<1e-8f)y=c>1e-8f?Mathf.Clamp01(e/c):0;
            else if(c<1e-8f)x=Mathf.Clamp01(-d/a);
            else
            {
                x=den>1e-8f?Mathf.Clamp01((b*e-c*d)/den):.5f;
                y=(b*x+e)/c;
                if(y<0){y=0;x=Mathf.Clamp01(-d/a);}
                else if(y>1){y=1;x=Mathf.Clamp01((b-d)/a);}
            }
            var first=p+u*x;var second=r+v*y;point=(first+second)*.5f;
            return (first-second).sqrMagnitude<=radius*radius;
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
