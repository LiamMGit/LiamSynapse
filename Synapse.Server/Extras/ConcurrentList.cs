using System.Collections;

namespace Synapse.Server.Extras;

// not true concurrency but good enough
public sealed class ConcurrentList<T>(List<T> list) : IList<T>, IReadOnlyList<T>, IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);

    public ConcurrentList() : this([])
    {
    }

    public ConcurrentList(IEnumerable<T> enumerable) : this(enumerable.ToList())
    {
    }

    public ConcurrentList(int count) : this(new List<T>(count))
    {
    }

    ~ConcurrentList()
    {
        Dispose(false);
    }

    public int Count => Read(() => list.Count);

    bool ICollection<T>.IsReadOnly => false;

    public T this[int index]
    {
        get => Read(() => list[index]);
        set => Write(() => list[index] = value);
    }

    public static ConcurrentList<T> Prefilled(int count, Func<int, T> func)
    {
        return new ConcurrentList<T>(Enumerable.Range(0, count).Select(func));
    }

    public void Add(T item)
    {
        Write(() => list.Add(item));
    }

    public void Clear()
    {
        Write(() => list.Clear());
    }

    public bool Contains(T item)
    {
        return Read(() => list.Contains(item));
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        Read(() => list.CopyTo(array, arrayIndex));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public int FindIndex(Predicate<T> match)
    {
        return Read(() => list.FindIndex(match));
    }

    public IEnumerator<T> GetEnumerator()
    {
        return Read(() => list.GetEnumerator());
    }

    public int IndexOf(T item)
    {
        return Read(() => list.IndexOf(item));
    }

    public void Insert(int index, T item)
    {
        Write(() => list.Insert(index, item));
    }

    public bool Remove(T item)
    {
        return Write(() => list.Remove(item));
    }

    public void RemoveAt(int index)
    {
        Write(() => list.RemoveAt(index));
    }

    public bool Remove(Predicate<T> predicate)
    {
        return Write(
            () =>
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (!predicate(list[i]))
                    {
                        continue;
                    }

                    list.RemoveAt(i);
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
        return Read(() => list.GetEnumerator());
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
