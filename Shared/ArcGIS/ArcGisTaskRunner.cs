using System;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace XIAOFUTools.Shared.ArcGis
{
    internal interface IArcGisTaskRunner
    {
        Task Run(Action action);
        Task<T> Run<T>(Func<T> function);
    }

    internal sealed class ArcGisTaskRunner : IArcGisTaskRunner
    {
        public Task Run(Action action) => QueuedTask.Run(action ?? throw new ArgumentNullException(nameof(action)));

        public Task<T> Run<T>(Func<T> function) => QueuedTask.Run(function ?? throw new ArgumentNullException(nameof(function)));
    }
}
