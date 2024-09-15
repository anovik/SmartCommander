using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading;

public class ThreadSafeObservableCollection<T> : ObservableCollection<T>
{
    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

    public ThreadSafeObservableCollection() { }

    public ThreadSafeObservableCollection(IEnumerable<T> collection) : base(collection) { }

    protected override void InsertItem(int index, T item)
    {
        _lock.EnterWriteLock();
        try
        {
            base.InsertItem(index, item);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    protected override void RemoveItem(int index)
    {
        _lock.EnterWriteLock();
        try
        {
            base.RemoveItem(index);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    protected override void SetItem(int index, T item)
    {
        _lock.EnterWriteLock();
        try
        {
            base.SetItem(index, item);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    protected override void ClearItems()
    {
        _lock.EnterWriteLock();
        try
        {
            base.ClearItems();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void AddRange(IEnumerable<T> items)
    {
        _lock.EnterWriteLock();
        try
        {
            foreach (var item in items)
            {
                base.InsertItem(Count, item);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}