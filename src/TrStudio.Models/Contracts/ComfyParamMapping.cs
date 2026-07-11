namespace TrStudio.Models.Contracts;

/// <summary>
/// One admin-chosen mapping from a parameterizable workflow input to a registry parameter
/// (FN-044/BRD-35). <see cref="ParamKey"/> selects the source input (<c>{NodeId}.{InputName}</c>);
/// the remaining fields shape the exposed <see cref="TrStudio.Models.Entities.ModelParamSpec"/>.
/// A mapping whose <see cref="ParamKey"/> is not an editable scalar input is ignored, so internal
/// and link inputs can never be exposed.
/// </summary>
public class ComfyParamMapping
{
    /// <summary>The source input key, formed as <c>{NodeId}.{InputName}</c> (e.g. "6.text").</summary>
    public string ParamKey { get; set; } = string.Empty;

    /// <summary>The registry parameter key the creator UI binds to; defaults to <see cref="ParamKey"/> when empty.</summary>
    public string? Key { get; set; }

    /// <summary>The parameter type; defaults to the input's inferred type when null.</summary>
    public string? Type { get; set; }

    /// <summary>Whether the parameter is required for a job to be submitted.</summary>
    public bool Required { get; set; }

    /// <summary>The default value; defaults to the input's current value when null.</summary>
    public string? DefaultValue { get; set; }

    /// <summary>The allowed values when the parameter type is <c>enum</c>.</summary>
    public string[]? Values { get; set; }

    /// <summary>The minimum allowed value for numeric types, if constrained.</summary>
    public double? Min { get; set; }

    /// <summary>The maximum allowed value for numeric types, if constrained.</summary>
    public double? Max { get; set; }

    /// <summary>The UI label; defaults to the input name when null.</summary>
    public string? Label { get; set; }
}
