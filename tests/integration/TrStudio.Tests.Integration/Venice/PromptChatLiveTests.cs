using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TrStudio.Engine.Infrastructure.Security;
using TrStudio.InProcess;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Enums;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;
using Xunit.Abstractions;

namespace TrStudio.Tests.Integration.Venice;

/// <summary>
/// REQ-RAG-005, text only, live (owner run of 17 Nov 2026): a Creator sends a message on Prompt studio
/// and the picked text model's own reply comes back. The engine runs in process exactly as in the
/// desktop app; the picked registry row points at Venice's OpenAI-compatible chat endpoint with the
/// cheapest plain text model, and its key is the Venice key from testsettings.Local.json, stored in the
/// engine's encrypted key store as an admin would on Registry &amp; providers. Paid (a fraction of a cent):
/// only with <c>--filter Category=Live</c>.
/// </summary>
[Trait("Category", "Live")]
public class PromptChatLiveTests(ITestOutputHelper aOutput)
{
    private const string ChatModel = "mistral-small-3-2-24b-instruct";

    [VeniceLiveFact(DisplayName = "REQ-RAG-005 TextOnlyLiveReplyFromConfiguredLlm")]
    public async Task TextOnlyLiveReplyFromConfiguredLlm()
    {
        var vFolder = Path.Combine(Path.GetTempPath(), "trstudio-rag005-" + Guid.NewGuid().ToString("N")[..8]);
        var vHost = EngineHost.Create(new EngineHostOptions
        {
            DataFolder = vFolder,
            SignIn = SignInSettings.None,
            KeyProtector = new MemoryKeyProtector(),
            WorkerCount = 1,
            StartWorkers = false
        });
        await vHost.StartAsync();
        try
        {
            var vServices = vHost.Services;
            await vServices.GetRequiredService<IRegistryRepository>().UpsertAsync(new RegistryModel
            {
                Id = "venice-chat-live",
                DisplayName = "Venice text (live check)",
                Task = GenerationTaskType.TextChat,
                ProviderKind = ProviderKind.TextLlm,
                Tier = GenerationTier.Fast,
                Endpoint = TestSettings.Get("Venice:BaseUrl"),
                ChatModel = ChatModel,
                VisionCapable = false,
                IsEnabled = true,
                IsReady = true
            });
            await vServices.GetRequiredService<IKeyStore>().StoreAsync("venice-chat-live", TestSettings.Get("Venice:ApiKey"));

            var vToken = "t-" + Guid.NewGuid().ToString("N");
            vHost.Sessions.SignIn(vToken, "creator-live", null, UserRole.Creator);
            using var vClient = new HttpClient(new InProcessEngineHandler(vHost), disposeHandler: true) { BaseAddress = InProcessEngineHandler.BaseAddress };
            vClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", vToken);

            var vCreate = await vClient.PostAsJsonAsync("/api/v1/prompt-chat/sessions", new PromptChatSessionCreateRequest { ModelId = "venice-chat-live" });
            Assert.True(vCreate.IsSuccessStatusCode, await vCreate.Content.ReadAsStringAsync());
            var vSessionId = JsonDocument.Parse(await vCreate.Content.ReadAsStringAsync()).RootElement.GetProperty("sessionId").GetGuid();

            var vPost = await vClient.PostAsJsonAsync($"/api/v1/prompt-chat/sessions/{vSessionId}/messages",
                new PromptChatPostMessageRequest { Message = "Write one short image prompt (under 25 words) for a red paper boat on a calm pond at sunrise." });
            var vText = await vPost.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, vPost.StatusCode);
            var vReply = JsonSerializer.Deserialize<PromptChatPostMessageResponse>(vText, new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } })!;
            aOutput.WriteLine($"model {ChatModel}; degraded {vReply.Degraded}; reply: {vReply.AssistantMessage?.Content}");

            Assert.False(vReply.Degraded, "the reply came from the degraded path, not the configured model");
            var vContent = vReply.AssistantMessage!.Content;
            Assert.False(string.IsNullOrWhiteSpace(vContent));
            Assert.Contains("boat", vContent, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await vHost.DisposeAsync();
            try
            {
                Directory.Delete(vFolder, recursive: true);
            }
            catch (IOException)
            {
                // a temporary folder
            }
        }
    }

    /// <summary>A random unlock key held in memory for this test only.</summary>
    private sealed class MemoryKeyProtector : IKeyProtector
    {
        private readonly byte[] objKey = RandomNumberGenerator.GetBytes(32);

        public string Description => "a test key in memory";

        public byte[] GetOrCreateDataKey() => objKey;
    }
}
