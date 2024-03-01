using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;

namespace Synapse.Extras
{
    internal static class WebRequestExtensions
    {
        internal static void RequestSprite(string url, Action<Sprite> action)
        {
            UnityWebRequestTexture.GetTexture(url).SendAndVerify(n =>
            {
                Texture2D tex = DownloadHandlerTexture.GetContent(n);
                Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 256f, 0U, SpriteMeshType.FullRect, new Vector4(0f, 0f, 0f, 0f), false);
                action(sprite);
            });
        }

        internal static void SendAndVerify(this UnityWebRequest webRequest, Action<UnityWebRequest> action) =>
            webRequest.SendWebRequest().completed += n =>
            {
                UnityWebRequest www = ((UnityWebRequestAsyncOperation)n).webRequest;

                if (www.isHttpError)
                {
                    Plugin.Log.Error($"Failed to connect to [{www.url}], server returned an error response ({www.responseCode})");
                    return;
                }

                if (www.isNetworkError)
                {
                    Plugin.Log.Error($"Failed to connect to [{www.url}], network error ({www.error})");
                    return;
                }

                action(www);
            };
    }
}
