namespace Larchik.Infrastructure.Jobs;

public interface IBackgroundJobHandler
{
    string JobType { get; }
    Task<JobExecutionResult> ExecuteAsync(string payloadJson, CancellationToken cancellationToken);
}
