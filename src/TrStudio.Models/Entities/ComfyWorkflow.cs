namespace TrStudio.Models.Entities;

/// <summary>
/// An imported ComfyUI workflow owned by an admin (FN-043/BRD-34). The workflow is the parent of
/// one or more <see cref="ComfyWorkflowVersion"/> rows; a registered model pins exactly one of
/// those versions. Scoped to the importing admin's AppManager user id (owner filter, BRD-81).
/// </summary>
public class ComfyWorkflow
{
    /// <summary>Stable identifier for the imported workflow.</summary>
    public Guid WorkflowId { get; set; }

    /// <summary>The AppManager user id of the admin that imported the workflow (owner filter).</summary>
    public string OwnerUserId { get; set; } = string.Empty;

    /// <summary>Human-readable workflow name shown in the ComfyUI import screen.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the workflow was first imported.</summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>UTC timestamp when the workflow was last updated (new version added).</summary>
    public DateTime UpdatedUtc { get; set; }
}
