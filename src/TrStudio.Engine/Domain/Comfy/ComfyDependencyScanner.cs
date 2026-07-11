using TrStudio.Models.Entities;

namespace TrStudio.Engine.Domain.Comfy;

/// <summary>
/// Scans a parsed workflow for the custom nodes and model files it requires and reports which of
/// them the current instance cannot confirm are installed (FN-047/BRD-38). Without a live ComfyUI
/// environment there is no installed-node/model inventory to check against, so every non-built-in
/// node and every referenced model file is reported as unconfirmed with an actionable install
/// hint; actual installation needs the ComfyUI env and is therefore reported, not performed.
/// </summary>
public class ComfyDependencyScanner
{
    private readonly ComfyWorkflowParser objParser;

    /// <summary>
    /// Initialises the scanner over the workflow parser.
    /// </summary>
    /// <param name="aParser">The parser producing the node graph.</param>
    public ComfyDependencyScanner(ComfyWorkflowParser aParser)
    {
        objParser = aParser;
    }

    /// <summary>
    /// Scans the workflow and produces the dependency report.
    /// </summary>
    /// <param name="aWorkflowJson">The workflow JSON to scan.</param>
    /// <returns>The report; an empty report for malformed JSON.</returns>
    public ComfyDependencyReport Scan(string aWorkflowJson)
    {
        var vReport = new ComfyDependencyReport();
        var vParsed = objParser.Parse(aWorkflowJson);
        if (!vParsed.Ok)
        {
            return vReport;
        }

        var vSeenNodes = new HashSet<string>(StringComparer.Ordinal);
        var vSeenFiles = new HashSet<string>(StringComparer.Ordinal);

        foreach (var vNode in vParsed.Nodes)
        {
            if (!ComfyBuiltinNodes.IsBuiltin(vNode.ClassType) && vSeenNodes.Add(vNode.ClassType))
            {
                vReport.RequiredCustomNodes.Add(vNode.ClassType);
                vReport.MissingCustomNodes.Add(new ComfyMissingDependency
                {
                    Name = vNode.ClassType,
                    Kind = "custom-node",
                    InstallHint =
                        $"Install the custom node providing '{vNode.ClassType}' via ComfyUI-Manager " +
                        "(or git-clone it into ComfyUI/custom_nodes) and restart ComfyUI."
                });
            }

            foreach (var vInput in vNode.Inputs)
            {
                if (vInput.Kind != "scalar" || !ComfyBuiltinNodes.IsModelFileInput(vInput.Name)
                    || string.IsNullOrWhiteSpace(vInput.Value) || !vSeenFiles.Add(vInput.Value!))
                {
                    continue;
                }

                vReport.RequiredModelFiles.Add(vInput.Value!);
                vReport.MissingModelFiles.Add(new ComfyMissingDependency
                {
                    Name = vInput.Value!,
                    Kind = "model-file",
                    InstallHint =
                        $"Place '{vInput.Value}' under the matching ComfyUI models folder " +
                        $"(input '{vInput.Name}' on node '{vNode.NodeId}') or install it from a configured model source."
                });
            }
        }

        return vReport;
    }
}
