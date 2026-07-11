namespace TrStudio.Models.Entities;

/// <summary>
/// A declarative parameter specification for a registry model. The UI renders a control for
/// each spec with no code change, so "add a model" is an admin action (Architecture §6, NFR-09).
/// </summary>
public class ModelParamSpec
{
    /// <summary>The parameter key as passed to the provider (e.g. "sourceImage", "resolution").</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The parameter data type. One of: <c>text</c>, <c>int</c>, <c>float</c>, <c>enum</c>,
    /// <c>bool</c>, <c>image</c>, <c>audio</c>, <c>mask</c>.
    /// </summary>
    public string Type { get; set; } = "text";

    /// <summary>Whether the parameter must be supplied for a job to be submitted.</summary>
    public bool Required { get; set; }

    /// <summary>The default value rendered into the UI control, if any.</summary>
    public string? DefaultValue { get; set; }

    /// <summary>The allowed values when <see cref="Type"/> is <c>enum</c>.</summary>
    public string[]? Values { get; set; }

    /// <summary>The minimum allowed value for numeric types, if constrained.</summary>
    public double? Min { get; set; }

    /// <summary>The maximum allowed value for numeric types, if constrained.</summary>
    public double? Max { get; set; }

    /// <summary>Human-readable label for the UI control; falls back to <see cref="Key"/> when null.</summary>
    public string? Label { get; set; }
}
