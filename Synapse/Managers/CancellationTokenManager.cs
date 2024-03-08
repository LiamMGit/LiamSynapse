using System;
using System.Threading;
using JetBrains.Annotations;

namespace Synapse.Managers
{
    [UsedImplicitly]
    internal sealed class CancellationTokenManager : IDisposable
    {
        private CancellationTokenSource _tokenSource = new();

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
            _tokenSource.Cancel();
            _tokenSource.Dispose();
            _tokenSource = new CancellationTokenSource();
            return _tokenSource.Token;
        }
    }
}
