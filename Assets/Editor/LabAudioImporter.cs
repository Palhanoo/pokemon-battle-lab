using System.IO;
using System.Linq;
using BattleLab;
using UnityEditor;
using UnityEngine;
public static class LabAudioImporter
{
    public static void Import()
    {
        const string path="Assets/Resources/LabAudio.asset";
        var catalog=AssetDatabase.LoadAssetAtPath<LabAudio>(path);
        if(!catalog){catalog=ScriptableObject.CreateInstance<LabAudio>();AssetDatabase.CreateAsset(catalog,path);}
        var index=JsonUtility.FromJson<LabAudio.Index>(File.ReadAllText("Assets/NativeAudio/index.json"));
        catalog.moves=index.moves;
        foreach(var m in catalog.moves)m.clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/NativeAudio/"+m.sound+".wav");
        catalog.cries=index.cries.Select(s=>new LabAudio.Cry{species=s,clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/NativeAudio/CRY_"+s+".wav")}).ToArray();
        if(catalog.moves.Any(m=>!m.clip))throw new System.InvalidOperationException("Missing attack audio");
        EditorUtility.SetDirty(catalog);Debug.Log($"LAB AUDIO {catalog.moves.Length} moves, {catalog.cries.Length} cries");
    }
}
public class LabAudioAssetImport:AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if(!assetPath.StartsWith("Assets/NativeAudio/"))return;
        var importer=(AudioImporter)assetImporter;
        var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.8f;importer.defaultSampleSettings=settings;
        importer.forceToMono=true;
    }
}
