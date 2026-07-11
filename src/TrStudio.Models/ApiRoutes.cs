namespace TrStudio.Models;

/// <summary>
/// The canonical route constants for the TrStudioApi surface. Both Blazor apps and the API
/// endpoint mappers reference these so routes never drift between client and server.
/// </summary>
public static class ApiRoutes
{
    /// <summary>Generation job endpoints (submit, status, list, cancel).</summary>
    public const string Jobs = "/api/v1/jobs";

    /// <summary>Generated asset endpoints (gallery listing, download, delete).</summary>
    public const string Assets = "/api/v1/assets";

    /// <summary>Creator project endpoints.</summary>
    public const string Projects = "/api/v1/projects";

    /// <summary>Model/provider registry endpoints (admin-scoped).</summary>
    public const string Registry = "/api/v1/admin/registry";

    /// <summary>Provider API key endpoints (admin-scoped; keys are write-only).</summary>
    public const string Providers = "/api/v1/admin/providers";

    /// <summary>Model source endpoints (admin-scoped).</summary>
    public const string Sources = "/api/v1/admin/sources";

    /// <summary>First-run environment setup endpoints (admin-scoped).</summary>
    public const string Setup = "/api/v1/admin/setup";

    /// <summary>Host hardware capability detection endpoints (admin-scoped).</summary>
    public const string Capabilities = "/api/v1/admin/capabilities";

    /// <summary>Model install endpoints (admin-scoped).</summary>
    public const string Installs = "/api/v1/admin/installs";

    /// <summary>Authentication endpoints (login/register/forgot-password), forwarded to AppManager.</summary>
    public const string Auth = "/api/v1/auth";

    /// <summary>Setup gate endpoint telling the UI whether the instance is usable.</summary>
    public const string SetupGate = "/api/v1/setup-gate";

    /// <summary>License status endpoint reporting the caller's AppManager license validity.</summary>
    public const string License = "/api/v1/license";

    /// <summary>SignalR hub pushing live job progress to the UI.</summary>
    public const string ProgressHub = "/hubs/progress";

    /// <summary>Selectable-model listing consumed by the studios (enabled models with params schema).</summary>
    public const string Models = "/api/v1/models";

    /// <summary>Generation log endpoints (history listing and one-click reproduce).</summary>
    public const string GenerationLog = "/api/v1/generation-log";

    /// <summary>Spend ledger + monthly-cap reporting endpoint (admin-scoped, REQ-FN-029/NFR-007).</summary>
    public const string Spend = "/api/v1/admin/spend";

    /// <summary>RunPod serverless ops endpoints (status/start/stop/idle-stop; admin-scoped, REQ-FN-028).</summary>
    public const string RunPod = "/api/v1/admin/runpod";

    /// <summary>Voice vertical endpoints (synthesize, preset-voice catalog, consent, clone, avatar handoff; FN-031..035).</summary>
    public const string Voice = "/api/v1/voice";

    /// <summary>Text-RAG endpoints (script generation, caption/subtitle text, per-user LLM token usage; RAG-001/002/003).</summary>
    public const string Rag = "/api/v1/rag";

    /// <summary>Abuse-reporting endpoint — any authenticated caller can file a report (SAFE-06 / FN-061).</summary>
    public const string AbuseReports = "/api/v1/abuse-reports";

    /// <summary>Recorded-consent endpoint for real-person face/voice work (SAFE-03 / FN-058).</summary>
    public const string Consent = "/api/v1/consent";

    /// <summary>Full generation audit trail (admin-scoped, SAFE-06 / FN-061).</summary>
    public const string AdminAudit = "/api/v1/admin/audit";

    /// <summary>Per-deployment safety policy surface incl. the NSFW-mode toggle (admin-scoped, SAFE-06 / FN-062).</summary>
    public const string AdminPolicy = "/api/v1/admin/policy";

    /// <summary>Admin operations dashboard (worker health, queue depth, spend, error rate; admin-scoped, FN-064/BRD-15).</summary>
    public const string AdminOps = "/api/v1/admin/ops";

    /// <summary>Side-by-side asset comparison endpoint (owner-scoped, FN-065/BRD-49).</summary>
    public const string Compare = "/api/v1/compare";

    /// <summary>Creator preset library endpoints (prompt/script/style-preset/voice-preset; owner-scoped, FN-066/BRD-50).</summary>
    public const string Presets = "/api/v1/presets";

    /// <summary>Account usage/spend + licence summary endpoint (owner-scoped, FN-068/BRD-52).</summary>
    public const string Account = "/api/v1/account";

    /// <summary>Owner-scoped external API-key issue/list/revoke surface (FN-069/BRD-66).</summary>
    public const string AccountApiKeys = "/api/v1/account/api-keys";

    /// <summary>Synthetic-character persona CRUD + RAG-backed bible (owner-scoped, FN-036/RAG-004/BRD-27/55).</summary>
    public const string Characters = "/api/v1/characters";

    /// <summary>Avatar pipeline endpoints: talking avatar, script→video, captions (owner-scoped, FN-037..040/BRD-28..31).</summary>
    public const string Avatar = "/api/v1/avatar";

    /// <summary>Video vertical endpoints: I2V/T2V generation, interpolate/upscale post-steps, edit/export (owner-scoped, FN-051..054/BRD-42..45).</summary>
    public const string Video = "/api/v1/video";

    /// <summary>ComfyUI pipeline management: import/parse, map, debug, save-as-model, version/pin, sandbox gate (admin-scoped, FN-043..050/BRD-34..41).</summary>
    public const string Comfy = "/api/v1/admin/comfy";

    /// <summary>Prompt Studio chat sessions/messages: per-user session CRUD + post-message-to-reply (owner-scoped, FN-073/RAG-005/BRD-88..92).</summary>
    public const string PromptChat = "/api/v1/prompt-chat";

    /// <summary>Text-chat LLM picker list for the Prompt Studio — the registry's <c>text-chat</c> rows only (FN-074/UI-081/BRD-89).</summary>
    public const string TextChatModels = "/api/v1/models/text-chat";
}
