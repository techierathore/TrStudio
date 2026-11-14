namespace TrStudio.Engine.Domain.Jobs;

/// <summary>
/// The in-app job queue (phase 3, ADR-018): replaces Hangfire. A job is a row of
/// <c>GenerationJob</c>; enqueueing hands its id to the app's own workers.
/// </summary>
public interface IJobQueue
{
    /// <summary>Hands a stored job to the workers.</summary>
    /// <param name="aJobId">The job id.</param>
    /// <param name="aUserId">The owning user (the worker reads the row with the owner filter).</param>
    /// <param name="aResume">True when the job was running at Venice when the app closed and polling resumes.</param>
    void Enqueue(Guid aJobId, string aUserId, bool aResume = false);
}
