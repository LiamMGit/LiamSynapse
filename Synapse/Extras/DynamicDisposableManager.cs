using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Synapse.Extras;

// a disposable manager dedicated to dynamically created objects
// needed to prevent circular dependencies to with the original disposable manager
[UsedImplicitly]
internal class DynamicDisposableManager : IDisposable
{
    private readonly HashSet<IDisposable> _disposables = [];

    public void Dispose()
    {
        foreach (IDisposable disposable in _disposables)
        {
            disposable.Dispose();
        }
    }

    internal void Add(IDisposable disposable)
    {
        _disposables.Add(disposable);
    }
}
