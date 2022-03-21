using System;
using System.Threading.Tasks;

namespace Bit.Core.IntegrationTest
{
    public class TemporaryItem<T> : IAsyncDisposable
    {
        private readonly Func<Task<T>> _build;
        private readonly Func<T, Task> _cleanup;

        public T Item { get; private set; }
        public bool Built { get; private set; }

        public TemporaryItem(Func<Task<T>> build, Func<T, Task> cleanup)
        {
            _build = build;
            _cleanup = cleanup;
        }

        public async Task BuildAsync()
        {
            Item = await _build();
            Built = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (Built)
            {
                await _cleanup(Item);
            }
            Built = false;
        }
    }

    public static class TemporaryItem
    {
        public static async Task<TemporaryItem<T>> CreateAsync<T>(Func<Task<T>> build, Func<T, Task> cleanup)
        {
            var item = new TemporaryItem<T>(build, cleanup);
            await item.BuildAsync();
            return item;
        }
    }
}
