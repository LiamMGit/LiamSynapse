using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using SiraUtil.Logging;
using UnityEngine.Networking;
using Zenject;

namespace Synapse.Managers
{
    internal sealed class DownloadingManager : IDisposable
    {
        private readonly SiraLog _log;

        private CancellationTokenSource _tokenSource = new();

        [UsedImplicitly]
        [Inject]
        private DownloadingManager(SiraLog log)
        {
            _log = log;
        }

        public void Dispose()
        {
            _tokenSource.Dispose();
        }

        internal void Cancel()
        {
            _tokenSource.Cancel();
        }

        internal CancellationToken Reset()
        {
            _tokenSource.Dispose();
            _tokenSource = new CancellationTokenSource();
            return _tokenSource.Token;
        }

        internal async Task<bool> Download(string url, string unzipPath, Action<float>? progress, Action? unzipping, Action<float>? unzipProgress, Action<string>? error, CancellationToken token)
        {
            try
            {
                using UnityWebRequest www = UnityWebRequest.Get(url);
                www.SendWebRequest();
                while (!www.isDone)
                {
                    if (!token.IsCancellationRequested)
                    {
                        progress?.Invoke(www.downloadProgress);
                        await Task.Delay(100, token);
                        continue;
                    }

                    www.Abort();
                    _log.Debug("Download cancelled");
                    return false;
                }

#pragma warning disable CS0618
                if (www.isNetworkError || www.isHttpError)
                {
                    if (www.isNetworkError)
                    {
                        string errorMessage =
                            $"Network error while downloading\n{www.error}";
                        _log.Error(errorMessage);
                        error?.Invoke(errorMessage);
                    }
                    else if (www.isHttpError)
                    {
                        string errorMessage =
                            $"Server sent error response code while downloading\n({www.responseCode})";
                        _log.Error(errorMessage);
                        error?.Invoke(errorMessage);
                    }

                    return false;
                }
#pragma warning restore CS0618

                unzipping?.Invoke();

                using MemoryStream zipStream = new(www.downloadHandler.data);
                using ZipArchive zip = new(zipStream, ZipArchiveMode.Read, false);
                await Task.Run(
                    () =>
                    {
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
                                entry.ExtractToFile(fullPath, false);
                            }
                        }
                    },
                    token);
            }
            catch (Exception e)
            {
                string errorMessage =
                    $"Error downloading\n({e})";
                _log.Error(errorMessage);
                error?.Invoke(errorMessage);
                return false;
            }

            return true;
        }
    }
}
