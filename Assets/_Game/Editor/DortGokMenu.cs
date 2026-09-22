using System;
using System.Linq;
using System.Reflection;
using DortGok.Core;
using DortGok.Data;
using DortGok.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DortGok.EditorTools
{
    /// <summary>"Dört Gök" menüsü: gri kutu sahnesi, URP kurulumu ve veri varlıkları.</summary>
    public static class DortGokMenu
    {
        private const string ScenePath = "Assets/Scenes/GriKutu.unity";
        private const string UrpPackage = "com.unity.render-pipelines.universal";

        [MenuItem("Dört Gök/Gri Kutu Sahnesini Oluştur", priority = 1)]
        public static void CreateGrayboxScene()
        {
            EnsureFolder("Assets", "Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var go = new GameObject("Dört Gök");
            go.AddComponent<GameBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("Dört Gök: gri kutu sahnesi oluşturuldu → " + ScenePath + ". Play'e basarak oynayabilirsin.");
        }

        [MenuItem("Dört Gök/URP'yi Kur ve Ayarla", priority = 20)]
        public static void SetupUrp()
        {
            var assetType = FindType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset");
            var dataType = FindType("UnityEngine.Rendering.Universal.UniversalRendererData");
            if (assetType == null || dataType == null)
            {
                Debug.Log("Dört Gök: URP paketi yükleniyor… Yükleme bitince bu menüyü bir kez daha çalıştır.");
                UnityEditor.PackageManager.Client.Add(UrpPackage);
                return;
            }
            try
            {
                EnsureFolder("Assets", "Settings");
                var data = ScriptableObject.CreateInstance(dataType);
                AssetDatabase.CreateAsset(data, "Assets/Settings/DortGok_URP_Renderer.asset");
                var create = assetType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .First(m => m.Name == "Create" && m.GetParameters().Length == 1);
                var asset = (RenderPipelineAsset)create.Invoke(null, new object[] { data });
                AssetDatabase.CreateAsset(asset, "Assets/Settings/DortGok_URP.asset");
                GraphicsSettings.defaultRenderPipeline = asset;
                QualitySettings.renderPipeline = asset;
                AssetDatabase.SaveAssets();
                Debug.Log("Dört Gök: URP varlığı oluşturuldu ve Graphics/Quality ayarlarına atandı.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Dört Gök: URP varlığı otomatik oluşturulamadı (" + ex.Message + "). " +
                                 "Elle: Project penceresinde Create > Rendering > URP Asset (with Universal Renderer), " +
                                 "sonra Project Settings > Graphics ve Quality'de bu varlığı seç. Oyun Built-in hattında da çalışır.");
            }
        }

        [MenuItem("Dört Gök/Veri/Alan Etkisi Varlıklarını Üret", priority = 40)]
        public static void GenerateEffectAssets()
        {
            EnsureFolder("Assets/_Game/Data", "Effects");
            EnsureFolder("Assets/_Game/Data", "Resources");
            var library = AssetDatabase.LoadAssetAtPath<VisualLibrary>("Assets/_Game/Data/Resources/DortGokVisuals.asset");
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<VisualLibrary>();
                AssetDatabase.CreateAsset(library, "Assets/_Game/Data/Resources/DortGokVisuals.asset");
            }
            foreach (var def in EffectCatalog.All)
            {
                string path = "Assets/_Game/Data/Effects/" + def.Id + ".asset";
                var asset = AssetDatabase.LoadAssetAtPath<AreaEffectAsset>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<AreaEffectAsset>();
                    asset.Def = def;
                    asset.TileColor = Palette.Effect(def.Id);
                    AssetDatabase.CreateAsset(asset, path);
                }
                if (!library.Effects.Contains(asset)) library.Effects.Add(asset);
            }
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log("Dört Gök: " + EffectCatalog.All.Count() + " alan etkisi varlığı üretildi (Assets/_Game/Data/Effects).");
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }
    }
}
