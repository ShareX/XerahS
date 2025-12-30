using System;
using System.Threading.Tasks;
using System.Threading;
using ShareX.Avalonia.Core;

namespace ShareX.Avalonia.Core.Tasks
{
    public interface IJobProcessor
    {
        Task ProcessAsync(TaskInfo info, CancellationToken token);
    }
}
