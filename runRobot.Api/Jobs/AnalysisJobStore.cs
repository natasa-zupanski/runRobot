using System.Collections.Concurrent;

namespace runRobot.Api.Jobs;

public enum JobStatus { Queued, Running, Done, Failed }

public class AnalysisJob
{
    public Guid      JobId    { get; init; } = Guid.NewGuid();
    public JobStatus Status   { get; set;  } = JobStatus.Queued;
    public string    Progress { get; set;  } = "";
    public AnalysisResult? CoreResult { get; set; }
    public string?   Error    { get; set;  }
}

/// <summary>
/// In-memory store for analysis jobs. Holds results for the lifetime of the process.
/// Register as a singleton in DI.
/// </summary>
public class AnalysisJobStore
{
    private readonly ConcurrentDictionary<Guid, AnalysisJob> _jobs = new();

    public AnalysisJob Create()
    {
        var job = new AnalysisJob();
        _jobs[job.JobId] = job;
        return job;
    }

    public AnalysisJob? Get(Guid id) => _jobs.GetValueOrDefault(id);
}
