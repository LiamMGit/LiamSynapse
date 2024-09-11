using System.Collections;

namespace Synapse.Server.Extras;

// not true concurrency but good enough
public sealed class ConcurrentList<T> : IList<T>, IReadOnlyList<T>, IDisposable
{
    private readonly List<T> _list;
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);

    public ConcurrentList()
    {
        _list = new List<T>();
    }

    public ConcurrentList(IEnumerable<T> enumerable)
    {
        _list = enumerable.ToList();
    }

    public ConcurrentList(int count)
    {
        _list = new List<T>(count);
    }

    ~ConcurrentList()
    {
        Dispose(false);
    }

    public int Count => Read(() => _list.Count);

    bool ICollection<T>.IsReadOnly => false;

    public T this[int index]
    {
        get => Read(() => _list[index]);
        set => Write(() => _list[index] = value);
    }

    public static ConcurrentList<T?> Prefilled(int count)
    {
        return new ConcurrentList<T?>(Enumerable.Range(0, count).Select<int, T?>(_ => default));
    }

    public void Add(T item)
    {
        Write(() => _list.Add(item));
    }

    public void Clear()
    {
        Write(() => _list.Clear());
    }

    public bool Contains(T item)
    {
        return Read(() => _list.Contains(item));
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        Read(() => _list.CopyTo(array, arrayIndex));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public int FindIndex(Predicate<T> match)
    {
        return Read(() => _list.FindIndex(match));
    }

    public IEnumerator<T> GetEnumerator()
    {
        return Read(() => _list.GetEnumerator());
    }

    public int IndexOf(T item)
    {
        return Read(() => _list.IndexOf(item));
    }

    public void Insert(int index, T item)
    {
        Write(() => _list.Insert(index, item));
    }

    public bool Remove(T item)
    {
        return Write(() => _list.Remove(item));
    }

    public void RemoveAt(int index)
    {
        Write(() => _list.RemoveAt(index));
    }

    public bool Remove(Predicate<T> predicate)
    {
        return Write(
            () =>
            {
                for (int i = 0; i < _list.Count; i++)
                {
                    if (!predicate(_list[i]))
                    {
                        continue;
                    }

                    _list.RemoveAt(i);
                    return true;
                }

                return false;
            });
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _lock.Dispose();
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return Read(() => _list.GetEnumerator());
    }

    private void Read(Action action)
    {
        _lock.EnterReadLock();
        try
        {
            action();
        }
        finally
        {
            if (_lock.IsReadLockHeld)
            {
                _lock.ExitReadLock();
            }
        }
    }

    private TRead Read<TRead>(Func<TRead> action)
    {
        _lock.EnterReadLock();
        try
        {
            return action();
        }
        finally
        {
            if (_lock.IsReadLockHeld)
            {
                _lock.ExitReadLock();
            }
        }
    }

    private void Write(Action action)
    {
        _lock.EnterWriteLock();
        try
        {
            action();
        }
        finally
        {
            if (_lock.IsWriteLockHeld)
            {
                _lock.ExitWriteLock();
            }
        }
    }

    private TWrite Write<TWrite>(Func<TWrite> action)
    {
        _lock.EnterWriteLock();
        try
        {
            return action();
        }
        finally
        {
            if (_lock.IsWriteLockHeld)
            {
                _lock.ExitWriteLock();
            }
        }
    }
}
