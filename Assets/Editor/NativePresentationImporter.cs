using System.Collections.Generic;
using System.IO;
using System.Linq;
using BattleLab;
using BattleLab.Native;
using UnityEditor;
using UnityEngine;

public static class NativePresentationImporter
{
    static T Catalog<T>(string name) where T:ScriptableObject
    {
        string path="Assets/Resources/"+name+".asset";
        var c=AssetDatabase.LoadAssetAtPath<T>(path);
        if(!c){c=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(c,path);}return c;
    }
    public static void Import()
    {
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/NativeParticle.mat");
        if(!material){material=new Material(Shader.Find("Hoenn/VioletParticle"));AssetDatabase.CreateAsset(material,"Assets/Resources/NativeParticle.mat");}
        var za=Catalog<VioletFxCatalog>("NativeZA");za.material=material;za.moves.Clear();za.textures.Clear();
        string dir="Assets/NativeFx/ZA";
        var index=(Dictionary<string,object>)MiniJson.Parse(File.ReadAllText(dir+"/index.json"));
        foreach(var pair in (Dictionary<string,object>)index["moves"])
        {
            string folder=dir+"/"+pair.Value;
            za.moves.Add(new VioletFxCatalog.Move{id=pair.Key,folder=(string)pair.Value,
                timeline=AssetDatabase.LoadAssetAtPath<TextAsset>(folder+"/move.json"),
                sets=Directory.GetFiles(folder,"*.json").Where(p=>Path.GetFileName(p)!="move.json").Select(p=>AssetDatabase.LoadAssetAtPath<TextAsset>(p.Replace('\\','/'))).ToArray()});
        }
        za.textures.AddRange(Directory.GetFiles(dir+"/textures","*.png").Select(p=>AssetDatabase.LoadAssetAtPath<Texture2D>(p.Replace('\\','/'))));
        var bdsp=Catalog<BdspFxCatalog>("NativeBDSP");bdsp.material=material;bdsp.moves.Clear();bdsp.prefabs.Clear();bdsp.textures.Clear();bdsp.locators.Clear();
        dir="Assets/NativeFx/BDSP";index=(Dictionary<string,object>)MiniJson.Parse(File.ReadAllText(dir+"/index.json"));
        foreach(var pair in (Dictionary<string,object>)index["moves"])
            bdsp.moves.Add(new BdspFxCatalog.Move{id=pair.Key,sequence=AssetDatabase.LoadAssetAtPath<TextAsset>(dir+"/"+pair.Value+"/move.json")});
        foreach(var pair in (Dictionary<string,object>)index["locators"])
            bdsp.locators.Add(new BdspFxCatalog.Locator{node=int.Parse(pair.Key),name=(string)pair.Value});
        bdsp.prefabs.AddRange(Directory.GetFiles(dir+"/prefabs","*.json",SearchOption.AllDirectories).Select(p=>AssetDatabase.LoadAssetAtPath<TextAsset>(p.Replace('\\','/'))));
        bdsp.textures.AddRange(Directory.GetFiles(dir+"/textures","*.png").Select(p=>AssetDatabase.LoadAssetAtPath<Texture2D>(p.Replace('\\','/'))));
        var timing=Catalog<NativeTiming>("NativeTiming");timing.rows=JsonUtility.FromJson<NativeTiming.Data>(File.ReadAllText("Assets/NativeFx/za-timing.json")).rows;
        foreach(var c in new ScriptableObject[]{za,bdsp,timing})EditorUtility.SetDirty(c);
        Debug.Log($"LAB NATIVE PRESENTATION ZA={za.moves.Count}/{za.textures.Count} textures BDSP={bdsp.moves.Count}/{bdsp.prefabs.Count} prefabs/{bdsp.textures.Count} textures");
    }
}
public class NativeFxTextureImport:AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/NativeFx/"))return;
        var imp=(TextureImporter)assetImporter;imp.sRGBTexture=false;imp.mipmapEnabled=true;
        imp.wrapMode=TextureWrapMode.Clamp;imp.alphaSource=TextureImporterAlphaSource.FromInput;imp.textureCompression=TextureImporterCompression.CompressedHQ;
        var bytes=File.ReadAllBytes(assetPath);
        if(bytes.Length>25&&bytes[25]==0)
        {imp.textureType=TextureImporterType.SingleChannel;var settings=new TextureImporterSettings();imp.ReadTextureSettings(settings);settings.singleChannelComponent=TextureImporterSingleChannelComponent.Red;imp.SetTextureSettings(settings);}
    }
}
