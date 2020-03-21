using System;
using System.Threading;
using System.Threading.Tasks;

namespace TwitchToSpeech.Model
{
    public sealed class TaskQueue : IDisposable
    {
        private bool disposed;
        private readonly SemaphoreSlim semaphore;
        private CancellationTokenSource cancellationTokenSource;

        public Task Task { get; private set; }

        public TaskQueue()
        {
            disposed = false;
            semaphore = new SemaphoreSlim(1);
            cancellationTokenSource = new CancellationTokenSource();
            Task = Task.CompletedTask;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        public void Enqueue(Action<CancellationToken> action, bool cancelPrevious)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(TaskQueue));

            semaphore.Wait();
            try
            {
                var localSource = cancellationTokenSource;

                if (cancelPrevious)
                {
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Dispose();
                    localSource = cancellationTokenSource = new CancellationTokenSource();
                }

                Task = Task.ContinueWith(_ => action(localSource.Token), localSource.Token,
                    TaskContinuationOptions.DenyChildAttach, TaskScheduler.Default);
            }
            finally
            {
                semaphore.Release();
            }
        }

        public void EnqueueAsync(Func<CancellationToken, Task> action, bool cancelPrevious)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(TaskQueue));

            semaphore.Wait();
            try
            {
                var localSource = cancellationTokenSource;

                if (cancelPrevious)
                {
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Dispose();
                    localSource = cancellationTokenSource = new CancellationTokenSource();
                }

                Task = Task.ContinueWith(async _ => await action(localSource.Token), localSource.Token,
                    TaskContinuationOptions.DenyChildAttach, TaskScheduler.Default).Unwrap();
            }
            finally
            {
                semaphore.Release();
            }
        }

        public void Cancel()
        {
            if (disposed)
                return;

            semaphore.Wait();
            try
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = new CancellationTokenSource();
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
