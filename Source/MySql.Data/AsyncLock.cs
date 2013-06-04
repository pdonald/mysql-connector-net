#if ASYNC
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MySql.Data.MySqlClient
{
    // http://blogs.msdn.com/b/pfxteam/archive/2012/02/12/10266983.aspx
    internal class AsyncSemaphore
    {
        private readonly static Task completed = Task.FromResult(true);
        private readonly Queue<TaskCompletionSource<bool>> waiters = new Queue<TaskCompletionSource<bool>>();
        private int currentCount;

        public AsyncSemaphore(int initialCount)
        {
            if (initialCount < 0) 
                throw new ArgumentOutOfRangeException("initialCount");

            currentCount = initialCount;
        }

        public Task WaitAsync()
        {
            lock (waiters)
            {
                if (currentCount > 0)
                {
                    --currentCount;
                    return completed;
                }
                else
                {
                    var waiter = new TaskCompletionSource<bool>();
                    waiters.Enqueue(waiter);
                    return waiter.Task;
                }
            }
        }

        public void Release()
        {
            TaskCompletionSource<bool> toRelease = null;
            lock (waiters)
            {
                if (waiters.Count > 0)
                    toRelease = waiters.Dequeue();
                else
                    ++currentCount;
            }
            if (toRelease != null)
                toRelease.SetResult(true);
        }
    }

    // http://blogs.msdn.com/b/pfxteam/archive/2012/02/12/10266988.aspx
    internal class AsyncLock
    {
        private readonly AsyncSemaphore semaphore;
        private readonly Task<Releaser> releaser;

        public AsyncLock()
        {
            semaphore = new AsyncSemaphore(1);
            releaser = Task.FromResult(new Releaser(this));
        }

        public Task<Releaser> LockAsync()
        {
            var wait = semaphore.WaitAsync();
            return wait.IsCompleted ?
                releaser :
                wait.ContinueWith((_, state) => new Releaser((AsyncLock)state),
                    this, CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        internal struct Releaser : IDisposable
        {
            private readonly AsyncLock toRelease;

            internal Releaser(AsyncLock toRelease)
            {
                this.toRelease = toRelease;
            }

            public void Dispose()
            {
                if (toRelease != null)
                    toRelease.semaphore.Release();
            }
        } 
    }

    internal interface IDisposableAsync
    {
        Task DisposeAsync();
    }

    internal static class Async
    {
        public static Task Using<T>(Func<T> disposableAcquisition, Func<T, Task> taskFunc) where T : IDisposableAsync
        {
            T instance = disposableAcquisition();

            return taskFunc(instance)
                .ContinueWith(async task =>
                {
                    if (!ReferenceEquals(instance, null))
                    {
                        await instance.DisposeAsync();
                    }
                    return task;
                });
        }
    }  
}
#endif