namespace XerahS.Core.Tasks
{
    public interface IJobProcessor
    {
        Task ProcessAsync(TaskInfo info, CancellationToken token);
    }
}
