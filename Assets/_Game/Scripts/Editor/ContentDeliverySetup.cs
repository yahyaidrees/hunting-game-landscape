using System.IO;
using System.Text;
using Game.Core.Content;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Android;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets.Android;

namespace Game.Editor
{
    /// <summary>
    /// Addressables with Play Asset Delivery (plan section 5): the settings, the package's PAD initialisation and
    /// build script, and the Gate A test pack: group PAD_Test, delivered on demand, holding one texture under the
    /// key the AssetPackProbe loads. Content is built with every player build (the PAD build script for Android).
    /// Later each region gets its own group (R02 fast-follow, R03+ on demand).
    /// </summary>
    public static class ContentDeliverySetup
    {
        const string TestGroup = "PAD_Test";
        const string TestTexturePath = "Assets/_Game/Art/Content/T_PadTest.png";

        [MenuItem("Game/Build/Set Up Play Asset Delivery")]
        public static void SetUpMenu() => Debug.Log(SetUp());

        public static string SetUp()
        {
            var log = new StringBuilder();
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);

            // the package's own initialisation, with its "add the schema to existing groups" question answered yes
            var setup = typeof(PlayAssetDeliverySchema).Assembly.GetType("UnityEditor.AddressableAssets.Android.PlayAssetDeliverySetup");
            const System.Reflection.BindingFlags S = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            bool needsInit = (bool)setup.GetMethod("PlayAssetDeliveryNotInitialized", S).Invoke(null, null);
            if (needsInit)
            {
                var force = setup.GetProperty("ForcePADToExistingAddressablesGroup", S);
                force.SetValue(null, (bool?)true);
                try { setup.GetMethod("InitPlayAssetDelivery", S).Invoke(null, null); }
                finally { force.SetValue(null, null); }
                log.AppendLine("Play Asset Delivery initialised");
            }

            // the test pack: on demand, so loading its asset makes Play download it
            var group = settings.FindGroup(TestGroup)
                        ?? settings.CreateGroup(TestGroup, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(PlayAssetDeliverySchema));
            if (!group.HasSchema<PlayAssetDeliverySchema>()) group.AddSchema<PlayAssetDeliverySchema>();
            group.GetSchema<PlayAssetDeliverySchema>().AssetPackDeliveryType = DeliveryType.OnDemand;

            var texture = EnsureTestTexture();
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(TestTexturePath), group);
            entry.address = AssetPackProbe.TestKey;

            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            int pad = settings.DataBuilders.FindIndex(b => b is BuildScriptPlayAssetDelivery);
            if (pad >= 0) settings.ActivePlayerDataBuilderIndex = pad;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            log.AppendLine($"test pack {TestGroup}: on demand, key '{AssetPackProbe.TestKey}' ({texture.width}x{texture.height}); player data builder {(pad >= 0 ? "Play Asset Delivery" : "MISSING")}");
            return log.ToString();
        }

        // a 1024 texture of coloured noise (about 1 MB in the pack, so the download is real but quick)
        static Texture2D EnsureTestTexture()
        {
            if (!File.Exists(TestTexturePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(TestTexturePath));
                const int n = 1024;
                var px = new Color32[n * n];
                var rng = new System.Random(5);
                for (int i = 0; i < px.Length; i++) px[i] = new Color32((byte)rng.Next(256), (byte)rng.Next(256), (byte)rng.Next(256), 255);
                var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
                t.SetPixels32(px);
                File.WriteAllBytes(TestTexturePath, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(TestTexturePath);
                var ti = (TextureImporter)AssetImporter.GetAtPath(TestTexturePath);
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TestTexturePath);
        }
    }
}
