using System.Linq;
using BattleLab.Native;
using NUnit.Framework;
using UnityEngine;
public class HoennModelTests
{
    [Test] public void EarthquakeRetainsAnimatedSkinnedTerrainAndSubmeshMaterials()
    {
        var cat=Resources.Load<BdspFxCatalog>("NativeBDSP");
        var asset=cat.prefabs.Single(p=>p.name=="model_ew089_floor");
        var root=BdspModel.Build(asset.text,n=>cat.textures.Single(t=>t.name==n),cat.material,null);
        var before=new Mesh();var after=new Mesh();
        try
        {
            var model=root.GetComponent<BdspModel>();model.FitContact(0,true);
            var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>();Assert.AreEqual(38,skins.Length);
            Assert.IsFalse(skins.Any(s=>s.name.EndsWith(":p_dome03")),"cinematic blackout enclosure cannot cover the arena");
            Assert.IsTrue(skins.Any(s=>s.sharedMesh.subMeshCount>1));
            float movement=0;
            foreach(var skin in skins)
            {
                Assert.AreEqual(skin.sharedMesh.subMeshCount,skin.sharedMaterials.Length);
                Assert.IsTrue(skin.sharedMesh.boneWeights.All(w=>Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)<.001f));
                model.Sample(0);skin.BakeMesh(before);var a=before.vertices;
                model.Sample(.65f);skin.BakeMesh(after);var b=after.vertices;
                for(int i=0;i<a.Length;i++)movement=Mathf.Max(movement,Vector3.Distance(a[i],b[i]));
            }
            Assert.Greater(movement,.01f,"floor geometry must actually move, not merely have animation tracks");
        }
        finally
        {
            foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {Object.DestroyImmediate(r.sharedMesh);foreach(var m in r.sharedMaterials)Object.DestroyImmediate(m);}
            Object.DestroyImmediate(before);Object.DestroyImmediate(after);Object.DestroyImmediate(root);
        }
    }
}
