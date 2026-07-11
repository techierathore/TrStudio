using TrStudio.Models.Enums;

namespace TrStudio.Models.Contracts;

/// <summary>
/// Request body for submitting a generation job (<c>POST /api/v1/jobs</c>).
/// </summary>
public class JobSubmitRequest
{
    /// <summary>The registry model id to run.</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>The generation task to perform; must match the model's declared task.</summary>
    public GenerationTaskType Task { get; set; }

    /// <summary>The execution tier selected by the user.</summary>
    public GenerationTier Tier { get; set; }

    /// <summary>The job parameters as a JSON document matching the model's param specs.</summary>
    public string ParamsJson { get; set; } = "{}";

    /// <summary>The project to attach the job to, if any.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Explicit random seed for reproducibility; a random seed is used when null.</summary>
    public long? Seed { get; set; }
}
