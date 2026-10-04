using System;
using System.Threading;
using System.Threading.Tasks;

namespace AnimeStudio.App
{
    // Objects share their file reader, so reads from different threads must not overlap.
    public static class AssetIo
    {
        private static readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

        public static T Run<T>(Func<T> read, CancellationToken token = default)
        {
            gate.Wait(token);
            try { return read(); }
            finally { gate.Release(); }
        }

        public static void Run(Action read, CancellationToken token = default)
        {
            gate.Wait(token);
            try { read(); }
            finally { gate.Release(); }
        }

        public static Task<T> RunAsync<T>(Func<T> read, CancellationToken token = default)
            => Task.Run(() => Run(read, token), token);
    }
}
