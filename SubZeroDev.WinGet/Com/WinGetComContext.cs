using System.Collections.Concurrent;

using Microsoft.Management.Deployment;

namespace SubZeroDev.WinGet.Com;

/// <summary>
/// Owns the WinGet projection on one MTA thread. Projected objects never escape this context:
/// their agility is not a library assumption.
/// </summary>
internal sealed class WinGetComContext : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly ConcurrentDictionary<long, Action> _outstanding = new();
    private readonly ManualResetEventSlim _started = new();
    private readonly ManualResetEventSlim _stopped = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _gate = new();
    // Separate from _gate on purpose: the terminal disposal section blocks on _stopped, and the
    // pump needs _gate to post work, so holding _gate across that wait would deadlock.
    private readonly object _disposeGate = new();
    private readonly Thread _thread;
    private readonly WinGetFactory _factory;
    private readonly Lazy<PackageManager> _packageManager;
    private Exception? _startupFailure;
    private long _nextWorkId;
    private int _disposing;
    private int _finalizeQueued;
    private bool _disposed;

    internal WinGetComContext()
        : this(new WinGetFactory())
    {
    }

    internal WinGetComContext(WinGetFactory factory)
    {
        _factory = factory;
        // PublicationOnly, not the default ExecutionAndPublication: the default caches the
        // *exception* as well as the value, so a single transient COM activation failure would
        // poison this context — and therefore the whole process, since it is a DI singleton —
        // for its entire lifetime, with no recovery even after WinGet is repaired.
        // PublicationOnly re-runs the factory after a failure. Its usual drawback (racing
        // threads can each build an instance, one wins) cannot arise here: PackageManager is
        // only ever touched from the single owner thread that the pump below runs on.
        _packageManager = new Lazy<PackageManager>(factory.CreatePackageManager, LazyThreadSafetyMode.PublicationOnly);
        _thread = new Thread(Run) { IsBackground = true, Name = "SubZeroDev.WinGet COM context" };
        if (OperatingSystem.IsWindows())
        {
            _thread.SetApartmentState(ApartmentState.MTA);
        }

        _thread.Start();
        _started.Wait();
        if (_startupFailure is not null)
        {
            Dispose();
            throw new InvalidOperationException("Could not start the WinGet COM context.", _startupFailure);
        }
    }

    internal WinGetFactory Factory => _factory;

    internal PackageManager PackageManager => _packageManager.Value;

    internal Task<T> InvokeAsync<T>(Func<T> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Enqueue(() => Task.FromResult(action()), cancellationToken);
    }

    internal Task<T> InvokeAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Enqueue(action, cancellationToken);
    }

    /// <summary>Posts cancellation to the owner thread; callers must dispose the registration.</summary>
    internal IDisposable RegisterCancellation(CancellationToken cancellationToken, Action cancel)
    {
        ArgumentNullException.ThrowIfNull(cancel);

        // Same reason as Enqueue: don't read _shutdown.Token once disposal has released it.
        // A no-op registration is correct here — the work it would have cancelled is already
        // being torn down.
        if (Volatile.Read(ref _disposing) != 0)
        {
            return NullRegistration.Instance;
        }

        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        var registration = linked.Token.Register(() => TryPost(() =>
        {
            // Cancellation is best effort. A COM failure while cancelling must not terminate
            // the owner pump and strand unrelated work.
            try { cancel(); }
            catch { }
        }));
        return new CancellationRegistration(linked, registration);
    }

    private Task<T> Enqueue<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        // Bail before touching _shutdown.Token: once Dispose has released it, reading the token
        // throws synchronously, which would break the faulted-task contract the _gate check below
        // already establishes for work submitted after disposal.
        if (Volatile.Read(ref _disposing) != 0)
        {
            return Task.FromException<T>(new ObjectDisposedException(nameof(WinGetComContext)));
        }

        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var id = Interlocked.Increment(ref _nextWorkId);
        var started = 0;
        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        var effectiveCancellation = linked.Token;
        var registration = effectiveCancellation.Register(() =>
        {
            if (Volatile.Read(ref started) == 0)
            {
                result.TrySetCanceled(effectiveCancellation);
            }
        });

        _outstanding.TryAdd(id, () => result.TrySetCanceled(effectiveCancellation));
        lock (_gate)
        {
            if (Volatile.Read(ref _disposing) != 0 || _queue.IsAddingCompleted)
            {
                registration.Dispose();
                linked.Dispose();
                _outstanding.TryRemove(id, out _);
                result.TrySetException(new ObjectDisposedException(nameof(WinGetComContext)));
                TryFinalizeShutdown();
                return result.Task;
            }

            _queue.Add(async () =>
            {
                if (Interlocked.Exchange(ref started, 1) != 0 || result.Task.IsCompleted)
                {
                    Complete(id, registration, linked);
                    return;
                }

                try
                {
                    result.TrySetResult(await action());
                }
                catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested || exception.CancellationToken == cancellationToken)
                {
                    result.TrySetCanceled(effectiveCancellation);
                }
                catch (Exception exception)
                {
                    result.TrySetException(exception);
                }
                finally
                {
                    Complete(id, registration, linked);
                }
            });
        }
        return result.Task;
    }

    private void Complete(long id, CancellationTokenRegistration registration, CancellationTokenSource linked)
    {
        registration.Dispose();
        linked.Dispose();
        _outstanding.TryRemove(id, out _);
        TryFinalizeShutdown();
    }

    private void Run()
    {
        try
        {
            SynchronizationContext.SetSynchronizationContext(new QueueSynchronizationContext(this));
        }
        catch (Exception exception)
        {
            _startupFailure = exception;
        }
        finally
        {
            _started.Set();
        }

        foreach (var callback in _queue.GetConsumingEnumerable())
        {
            try { callback(); }
            catch { /* An unexpected posted callback must not kill the owner pump. */ }
        }
        _stopped.Set();
    }

    private bool TryPost(Action callback)
    {
        lock (_gate)
        {
            if (_queue.IsAddingCompleted)
            {
                return false;
            }

            try
            {
                _queue.Add(callback);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }

    private void TryFinalizeShutdown()
    {
        if (Volatile.Read(ref _disposing) == 0 || !_outstanding.IsEmpty || Interlocked.Exchange(ref _finalizeQueued, 1) != 0)
        {
            return;
        }

        if (!TryPost(FinalizeShutdown))
        {
            _stopped.Set();
        }
    }

    private void FinalizeShutdown()
    {
        if (!_outstanding.IsEmpty)
        {
            Interlocked.Exchange(ref _finalizeQueued, 0);
            return;
        }

        lock (_gate)
        {
            _queue.CompleteAdding();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposing, 1) == 0)
        {
            _shutdown.Cancel();
            foreach (var cancel in _outstanding.Values)
            {
                cancel();
            }
            TryFinalizeShutdown();
        }

        // Disposal from a COM callback must not join its own owner thread. The callback's
        // completion will drain the queue and stop the thread after it returns. That path also
        // cannot release the primitives below — the pump is still running on this very thread —
        // so it leaves them to finalization. Every other path (DI provider disposal, a directly
        // constructed client, the constructor's startup-failure cleanup) reaches the join.
        if (Environment.CurrentManagedThreadId == _thread.ManagedThreadId)
        {
            return;
        }

        // Serialized so concurrent disposers can't wait on, or join against, primitives another
        // disposer has already released; the loser returns without touching anything.
        lock (_disposeGate)
        {
            if (_disposed)
            {
                return;
            }

            _stopped.Wait();
            _thread.Join();

            // Past the join the pump is provably dead, so the owned primitives can go.
            // _queue under _gate, because TryPost adds to it while holding that lock.
            lock (_gate)
            {
                _queue.Dispose();
            }

            _started.Dispose();
            _stopped.Dispose();
            _shutdown.Dispose();
            _disposed = true;
        }
    }

    private sealed class QueueSynchronizationContext(WinGetComContext owner) : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) => owner.TryPost(() => callback(state));
    }

    private sealed class CancellationRegistration(CancellationTokenSource source, CancellationTokenRegistration registration) : IDisposable
    {
        public void Dispose()
        {
            registration.Dispose();
            source.Dispose();
        }
    }

    /// <summary>Handed out when the context is already shutting down; disposing it is a no-op.</summary>
    private sealed class NullRegistration : IDisposable
    {
        internal static readonly NullRegistration Instance = new();

        public void Dispose()
        {
        }
    }
}
