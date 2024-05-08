using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace Synapse.Extras
{
    internal static class MediaExtensions
    {
        internal static async Task<AssetBundle?> LoadFromFileAsync(string path, uint crc)
        {
            TaskCompletionSource<AssetBundle?> taskCompletionSource = new();
            AssetBundleCreateRequest bundleRequest = AssetBundle.LoadFromFileAsync(path, crc);
            bundleRequest.completed += _ =>
            {
                taskCompletionSource.SetResult(bundleRequest.assetBundle);
            };

            return await taskCompletionSource.Task;
        }

        internal static async Task<T> LoadAssetAsyncTask<T>(this AssetBundle assetBundle, string name)
            where T : Object
        {
            TaskCompletionSource<T> taskCompletionSource = new();
            AssetBundleRequest bundleRequest = assetBundle.LoadAssetAsync<T>(name);
            bundleRequest.completed += _ =>
            {
                if (bundleRequest.asset == null)
                {
                    throw new InvalidOperationException("Asset was null.");
                }

                if (bundleRequest.asset is not T asset)
                {
                    throw new InvalidOperationException($"Asset was not {typeof(T).Name}.");
                }

                taskCompletionSource.SetResult(asset);
            };

            return await taskCompletionSource.Task;
        }

        internal static async Task<Sprite> RequestSprite(string url, CancellationToken token)
        {
            UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
            await www.SendAndVerify(token);
            Texture2D tex = DownloadHandlerTexture.GetContent(www);
            return tex.GetSprite();
        }

        internal static Sprite GetEmbeddedResourceSprite(string path)
        {
            using Stream stream =
                typeof(MediaExtensions).Assembly.GetManifestResourceStream(path) ?? throw new InvalidOperationException();
            using MemoryStream memStream = new();
            stream.CopyTo(memStream);
            Texture2D tex = new(2, 2);
            tex.LoadImage(memStream.ToArray());
            return tex.GetSprite();
        }

        internal static Sprite GetSprite(this Texture2D tex)
        {
            return Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                256f,
                0U,
                SpriteMeshType.FullRect,
                new Vector4(0f, 0f, 0f, 0f),
                false);
        }

        internal static Task SendAndVerify(
            this UnityWebRequest www,
            CancellationToken token) => www.SendAndVerify(null, token);

        internal static async Task SendAndVerify(
            this UnityWebRequest www,
            Action<float>? progress,
            CancellationToken token)
        {
            www.SendWebRequest();
            while (!www.isDone)
            {
                if (token.IsCancellationRequested)
                {
                    www.Abort();
                    token.ThrowIfCancellationRequested();
                    return;
                }

                progress?.Invoke(www.downloadProgress);
                await Task.Delay(100, CancellationToken.None);
            }

            if (www.isHttpError)
            {
                throw new InvalidOperationException(
                    $"Failed to connect to [{www.url}], server returned an error response ({www.responseCode}).");
            }

            if (www.isNetworkError)
            {
                throw new InvalidOperationException($"Failed to connect to [{www.url}], network error ({www.error}).");
            }
        }

        internal static async Task DownloadAndSave(
            string url,
            string unzipPath,
            Action<float>? progress,
            Action? unzipping,
            Action<float>? unzipProgress,
            CancellationToken token)
        {
            using UnityWebRequest www = UnityWebRequest.Get(url);
            await www.SendAndVerify(progress, token);

            unzipping?.Invoke();

            using MemoryStream zipStream = new(www.downloadHandler.data);
            using ZipArchive zip = new(zipStream, ZipArchiveMode.Read, false);
            ZipArchiveEntry[] entries = zip.Entries.ToArray();
            for (int j = 0; j < entries.Length; j++)
            {
                unzipProgress?.Invoke((float)j / entries.Length);
                ZipArchiveEntry entry = entries[j];
                string fullPath = Path.GetFullPath(Path.Combine(unzipPath, entry.FullName));
                if (Path.GetFileName(fullPath).Length == 0)
                {
                    Directory.CreateDirectory(fullPath);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                    entry.ExtractToFile(fullPath, true);
                }
            }
        }

        internal static void Purge(this DirectoryInfo directory)
        {
            // cleanup
            if (!directory.Exists)
            {
                return;
            }

            try
            {
                foreach (FileInfo file in directory.GetFiles())
                {
                    file.Delete();
                }

                foreach (DirectoryInfo dir in directory.GetDirectories())
                {
                    dir.Delete(true);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"Exception while purging directory: [{directory}]");
                Plugin.Log.Error(e);
            }
        }
    }
}
