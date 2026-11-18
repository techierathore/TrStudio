using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TrStudio.Engine.Domain.Comfy;
using TrStudio.Models;
using TrStudio.Models.Enums;

namespace TrStudio.Tests.Unit.Desktop;

/// <summary>
/// REQ-FN-047, owner UAT bug of 16 Nov 2026: importing a real graph reported every model file missing,
/// because the dependency scan never asked ComfyUI what it holds. Now the scan reads the running
/// ComfyUI's <c>/object_info</c>: what ComfyUI has is not missing; what it lacks still is. (Kept out of
/// the Comfy test folder, whose five files REQ-NFR-021 pins unchanged.)
/// </summary>
public class ComfyInventoryTests
{
    // A graph naming a held checkpoint, a missing LoRA, a held custom node and a missing custom node.
    private const string Graph =
        "{\"4\":{\"class_type\":\"CheckpointLoaderSimple\",\"inputs\":{\"ckpt_name\":\"sd_xl_base_1.0.safetensors\"}}," +
        "\"5\":{\"class_type\":\"LoraLoader\",\"inputs\":{\"lora_name\":\"not_here.safetensors\",\"model\":[\"4\",0],\"clip\":[\"4\",1]}}," +
        "\"6\":{\"class_type\":\"IPAdapterApply\",\"inputs\":{\"weight\":0.8}}," +
        "\"7\":{\"class_type\":\"NotInstalledNode\",\"inputs\":{\"x\":1}}," +
        "\"9\":{\"class_type\":\"SaveImage\",\"inputs\":{\"images\":[\"4\",0]}}}";

    // ComfyUI's answer, one loader in the older list form and one in the newer COMBO form.
    private const string ObjectInfo =
        "{\"CheckpointLoaderSimple\":{\"input\":{\"required\":{\"ckpt_name\":[[\"sd_xl_base_1.0.safetensors\",\"v1-5.safetensors\"],{}]}}}," +
        "\"LoraLoader\":{\"input\":{\"required\":{\"lora_name\":[\"COMBO\",{\"options\":[\"detail.safetensors\"]}]}}}," +
        "\"IPAdapterApply\":{\"input\":{\"required\":{\"weight\":[\"FLOAT\",{}]}}}," +
        "\"SaveImage\":{\"input\":{\"required\":{}}}}";

    [Fact(DisplayName = "REQ-FN-047 WhatComfyUiHoldsIsNotReportedMissing")]
    public async Task WhatComfyUiHoldsIsNotReportedMissing()
    {
        var vComfy = new ComfyStandIn(ObjectInfo);
        await using var vEngine = await DesktopEngine.StartAsync(aConfigure: aServices =>
        {
            aServices.AddSingleton(new ComfyUiOptions { BaseUrl = "http://comfy.test:8188" });
            aServices.AddHttpClient(ComfyPipelineService.InventoryHttpClientName).ConfigurePrimaryHttpMessageHandler(() => vComfy);
        });
        using var vOwner = vEngine.Client(vEngine.SignIn(UserRole.Owner, "owner-1"));

        var vResponse = await vOwner.PostAsJsonAsync($"{ApiRoutes.Comfy}/dependencies", new { workflowJson = Graph });
        var vBody = await vResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, vResponse.StatusCode);
        Assert.Equal("/object_info", vComfy.Asked.Single());
        using var vReport = JsonDocument.Parse(vBody);
        var vRoot = vReport.RootElement;
        Assert.True(vRoot.GetProperty("checkedWithComfyUi").GetBoolean());
        Assert.Equal(new[] { "not_here.safetensors" }, Names(vRoot, "missingModelFiles"));
        Assert.Equal(new[] { "NotInstalledNode" }, Names(vRoot, "missingCustomNodes"));
        Assert.Contains("sd_xl_base_1.0.safetensors", vRoot.GetProperty("requiredModelFiles").EnumerateArray().Select(aItem => aItem.GetString()));
    }

    [Fact(DisplayName = "REQ-FN-047 WithoutComfyUiEveryNamedFileStaysMissingAndSaysSo")]
    public async Task WithoutComfyUiEveryNamedFileStaysMissingAndSaysSo()
    {
        await using var vEngine = await DesktopEngine.StartAsync(aConfigure: aServices =>
            aServices.AddSingleton(new ComfyUiOptions { BaseUrl = string.Empty }));
        using var vOwner = vEngine.Client(vEngine.SignIn(UserRole.Owner, "owner-1"));

        var vBody = await (await vOwner.PostAsJsonAsync($"{ApiRoutes.Comfy}/dependencies", new { workflowJson = Graph })).Content.ReadAsStringAsync();

        using var vReport = JsonDocument.Parse(vBody);
        Assert.False(vReport.RootElement.GetProperty("checkedWithComfyUi").GetBoolean());
        Assert.Equal(2, Names(vReport.RootElement, "missingModelFiles").Length);
        Assert.Equal(2, Names(vReport.RootElement, "missingCustomNodes").Length);
    }

    private static string[] Names(JsonElement aRoot, string aProperty)
        => aRoot.GetProperty(aProperty).EnumerateArray().Select(aItem => aItem.GetProperty("name").GetString()!).OrderBy(aName => aName).ToArray();

    private sealed class ComfyStandIn(string aObjectInfo) : HttpMessageHandler
    {
        public List<string> Asked { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage aRequest, CancellationToken aCancellationToken)
        {
            Asked.Add(aRequest.RequestUri!.AbsolutePath);
            return Task.FromResult(aRequest.RequestUri.AbsolutePath == "/object_info"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(aObjectInfo, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
