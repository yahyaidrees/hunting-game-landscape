using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Game.Core.Content
{
    /// <summary>
    /// Gate A check for Play Asset Delivery (plan section 5): once per launch on a phone, loads a small test asset
    /// that lives in its own on-demand asset pack (group PAD_Test), which makes Play download the pack, and logs
    /// "[pad] ..." with the result and the time it took. Replaced by the content delivery service (pack status,
    /// download progress, offline fallback, mobile data prompt) when the second region arrives.
    /// </summary>
    public static class AssetPackProbe
    {
        public const string TestKey = "pad-test";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Run()
        {
            if (Application.isEditor) return;
            var go = new GameObject("AssetPackProbe");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
        }

        sealed class Runner : MonoBehaviour
        {
            IEnumerator Start()
            {
                float started = Time.realtimeSinceStartup;
                var size = Addressables.GetDownloadSizeAsync(TestKey);
                yield return size;
                long bytes = size.Status == AsyncOperationStatus.Succeeded ? size.Result : -1;
                size.Release();

                var load = Addressables.LoadAssetAsync<Texture2D>(TestKey);
                yield return load;
                float seconds = Time.realtimeSinceStartup - started;
                if (load.Status == AsyncOperationStatus.Succeeded && load.Result)
                    Debug.Log($"[pad] test pack ok: {load.Result.width}x{load.Result.height} texture, {bytes} bytes to download before, {seconds:0.0} s");
                else
                    Debug.LogWarning($"[pad] test pack FAILED after {seconds:0.0} s: {load.OperationException?.Message ?? "no asset"}");
                load.Release();
                Destroy(gameObject);
            }
        }
    }
}
