using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BattleLab;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class BattleLabBuilder
{
    [MenuItem("Battle Lab/Set up isolated lab")]
    public static void Setup()
    {
        Directory.CreateDirectory("Assets/Resources");Directory.CreateDirectory("Assets/Scenes");
        AssetDatabase.Refresh();
        var catalog=AssetDatabase.LoadAssetAtPath<LabCatalog>("Assets/Resources/LabCatalog.asset");
        if(!catalog){catalog=ScriptableObject.CreateInstance<LabCatalog>();AssetDatabase.CreateAsset(catalog,"Assets/Resources/LabCatalog.asset");}
        catalog.data=JsonUtility.FromJson<LabData>(File.ReadAllText("Assets/Data/LabData.json"));
        catalog.models=(from source in new[]{"Arceus","ZA","BDSP"}
            from species in new[]{"PIKACHU","GENGAR","LUCARIO"}
            select Import(source,species)).ToArray();
        EditorUtility.SetDirty(catalog);
        NativePresentationImporter.Import();
        if(!AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/LabSurface.mat"))
            AssetDatabase.CreateAsset(new Material(Shader.Find("Universal Render Pipeline/Lit")),"Assets/Resources/LabSurface.mat");
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/URP.asset");
        GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
        QualitySettings.vSyncCount=0;QualitySettings.antiAliasing=4;
        PlayerSettings.companyName="BrunoLabs";PlayerSettings.productName="Pokemon Battle Lab";
        PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
        PlayerSettings.colorSpace=ColorSpace.Linear;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/BattleLab.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/BattleLab.unity",true)};
        AssetDatabase.SaveAssets();
        Debug.Log("LAB SETUP OK: 9 native models; 1 scene; no world/save dependency");
    }
    static ModelEntry Import(string source,string species)
    {
        string path=$"Assets/Models/{source}/{species}.glb";
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(!prefab)throw new InvalidOperationException("Missing native model "+path);
        var clips=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().ToArray();
        var roles=Regex.Matches(File.ReadAllText($"Assets/Models/{source}/{species}.clips.json"),"\"([^\"]+)\"\\s*:\\s*\"([^\"]+)\"")
            .Cast<Match>().Select(m=>new ClipRole{role=m.Groups[1].Value,clip=m.Groups[2].Value}).ToArray();
        var entry=new ModelEntry{source=source,species=species,prefab=prefab,clips=clips,roles=roles};
        foreach(var role in new[]{"idle","attack","special","damage","faint"})
            if(!entry.Role(role))throw new InvalidOperationException(path+" missing "+role);
        Debug.Log($"LAB MODEL {source}/{species}: {clips.Length} clips");return entry;
    }
    [MenuItem("Battle Lab/Build Windows review")]
    public static void Build()
    {
        Setup();Directory.CreateDirectory("Builds/DirectControl");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/BattleLab.unity"},
            locationPathName="Builds/DirectControl/PokemonBattleLab.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        Debug.Log($"LAB BUILD {report.summary.result} scenes=1 errors={report.summary.totalErrors} bytes={report.summary.totalSize}");
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Battle lab build failed");
    }
}
