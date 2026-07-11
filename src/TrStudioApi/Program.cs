using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using TrStudio.Engine.DependencyInjection;
using TrStudio.Engine.Infrastructure.Identity;
using TrStudio.Engine.Infrastructure.Observability;
using TrStudio.Models;
using TrStudioApi.Endpoints;
using TrStudioApi.Hubs;
using TrStudioApi.Infrastructure.Auth;
using TrStudioDb;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting TrStudioApi");

    var vBuilder = WebApplication.CreateBuilder(args);

    // Serilog is the logging pipeline (structured logging, NFR-05: no secrets in logs).
    vBuilder.Host.UseSerilog((aContext, aServices, aLoggerConfiguration) => aLoggerConfiguration
        .ReadFrom.Configuration(aContext.Configuration)
        .ReadFrom.Services(aServices)
        .Enrich.FromLogContext());

    // OpenTelemetry metrics/tracing (NFR-008/BRD-76). The custom "TrStudio" meter carries the
    // queue-depth, spend and job-latency instruments; the "TrStudio" activity source carries the
    // job-execution spans that give every job an end-to-end (submit → provider → asset) job-id
    // correlation. No secrets are recorded on metrics or spans.
    vBuilder.Services.AddOpenTelemetry()
        .WithTracing(aTracing => aTracing
            .AddAspNetCoreInstrumentation()
            .AddSource(TrStudioTelemetry.Name))
        .WithMetrics(aMetrics => aMetrics
            .AddAspNetCoreInstrumentation()
            .AddMeter(TrStudioTelemetry.Name));

    // JWT bearer auth (REQ-FN-012, cluster D). AppManager-issued tokens are validated remotely
    // via POST /AuthSvc/validate (the guide's documented contract for child apps) through
    // AppManagerTokenHandler. The symmetric dev-token fallback (AppManager:JwtSigningKey or the
    // Wave-0 placeholder key) stays enabled behind AppManager:AllowDevSymmetricTokens so the
    // other clusters' dev-token smokes keep working.
    var vSigningKey = vBuilder.Configuration["AppManager:JwtSigningKey"];
    if (string.IsNullOrWhiteSpace(vSigningKey))
    {
        vSigningKey = "TrStudioWave0PlaceholderSigningKeyReplacedByIdentityCluster";
    }

    // REQ-FN-069/BRD-66: external callers authenticate with an X-Api-Key alongside the AppManager
    // JWT bearer. A composite policy scheme routes each request to the API-key scheme when the
    // header is present, else to JWT — so job endpoints accept EITHER without weakening the JWT
    // path (which is unchanged below).
    vBuilder.Services
        .AddAuthentication(aOptions =>
        {
            aOptions.DefaultScheme = ApiKeyDefaults.SmartScheme;
            aOptions.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddPolicyScheme(ApiKeyDefaults.SmartScheme, "JWT bearer or X-Api-Key", aOptions =>
        {
            aOptions.ForwardDefaultSelector = aContext =>
                aContext.Request.Headers.ContainsKey(ApiKeyDefaults.HeaderName)
                    ? ApiKeyDefaults.Scheme
                    : JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer()
        .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyDefaults.Scheme, null);

    vBuilder.Services
        .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
        .Configure<AppManagerTokenHandler>((aOptions, aTokenHandler) =>
        {
            aOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(vSigningKey))
            };
            aOptions.TokenHandlers.Clear();
            aOptions.TokenHandlers.Add(aTokenHandler);
        });

    // Endpoints require an authenticated user by default; auth/swagger/health opt out explicitly.
    vBuilder.Services.AddAuthorization(aOptions =>
    {
        aOptions.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });

    // API versioning (v1 default).
    vBuilder.Services
        .AddApiVersioning(aOptions =>
        {
            aOptions.DefaultApiVersion = new ApiVersion(1, 0);
            aOptions.AssumeDefaultVersionWhenUnspecified = true;
            aOptions.ReportApiVersions = true;
        })
        .AddApiExplorer(aOptions =>
        {
            aOptions.GroupNameFormat = "'v'VVV";
            aOptions.SubstituteApiVersionInUrl = true;
        });

    // Swagger UI at /swagger (anonymous — served by middleware, not an authorized endpoint).
    vBuilder.Services.AddEndpointsApiExplorer();
    vBuilder.Services.AddSwaggerGen();

    // SignalR progress hub services (cluster A completes the hub).
    vBuilder.Services.AddSignalR();

    // Engine modules — one per feature cluster (the Wave-0 anti-conflict seam).
    vBuilder.Services.AddSpineModule(vBuilder.Configuration);
    vBuilder.Services.AddRegistryModule(vBuilder.Configuration);
    vBuilder.Services.AddSetupModule(vBuilder.Configuration);
    vBuilder.Services.AddIdentityModule(vBuilder.Configuration);
    vBuilder.Services.AddImageModule(vBuilder.Configuration);
    vBuilder.Services.AddWorkspaceModule(vBuilder.Configuration);
    vBuilder.Services.AddCostModule(vBuilder.Configuration);
    vBuilder.Services.AddRunPodModule(vBuilder.Configuration);
    vBuilder.Services.AddVoiceModule(vBuilder.Configuration);
    vBuilder.Services.AddRagModule(vBuilder.Configuration);
    vBuilder.Services.AddAvatarModule(vBuilder.Configuration);
    vBuilder.Services.AddVideoModule(vBuilder.Configuration);
    vBuilder.Services.AddComfyModule(vBuilder.Configuration);
    vBuilder.Services.AddPhase9Module(vBuilder.Configuration);
    vBuilder.Services.AddPromptChatModule(vBuilder.Configuration);

    // Non-disableable safety floors (SAFE-01..06 / FN-056..063). The Development-only sentinel test
    // seam is gated on the host environment so it is NEVER active in production.
    vBuilder.Services.AddSafetyModule(vBuilder.Configuration, vBuilder.Environment.IsDevelopment());

    // NOTE: Hangfire server/dashboard is intentionally NOT enabled yet — it needs its Postgres
    // schema, which cluster A (spine) owns. Only the package references ship in Wave-0.

    var vApp = vBuilder.Build();

    // DbUp schema migration MUST succeed before the API serves traffic (ADR-010).
    var vConnectionString = vApp.Configuration.GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(vConnectionString))
    {
        Log.Fatal("ConnectionStrings:Default is not configured — cannot start TrStudioApi.");
        return 1;
    }

    try
    {
        DatabaseMigrator.MigrateDatabase(vConnectionString, aMessage => Log.Information("DbUp: {Message}", aMessage));
    }
    catch (Exception vDbException)
    {
        Log.Fatal(vDbException,
            "Database migration failed. Verify PostgreSQL is running on the host/port in " +
            "ConnectionStrings:Default and the credentials are valid, then restart TrStudioApi.");
        return 1;
    }

    vApp.UseSerilogRequestLogging();

    vApp.UseSwagger();
    vApp.UseSwaggerUI();

    vApp.UseAuthentication();
    vApp.UseAuthorization();

    // Health probe (anonymous) — proves the host is up and migration completed.
    vApp.MapGet("/health", () => Results.Ok(new { status = "ok" }))
        .AllowAnonymous()
        .WithName("GetHealth");

    // Live job progress hub (cluster A completes the hub implementation).
    vApp.MapHub<ProgressHub>(ApiRoutes.ProgressHub);

    // Per-cluster endpoint mappers (the Wave-0 anti-conflict seam).
    vApp.MapSpineEndpoints();
    vApp.MapRegistryEndpoints();
    vApp.MapSetupEndpoints();
    vApp.MapIdentityEndpoints();
    vApp.MapImageEndpoints();
    vApp.MapWorkspaceEndpoints();
    vApp.MapPhase3Endpoints();
    vApp.MapVoiceEndpoints();
    vApp.MapRagEndpoints();
    vApp.MapCharacterEndpoints();
    vApp.MapAvatarEndpoints();
    vApp.MapVideoEndpoints();
    vApp.MapSafetyEndpoints();
    vApp.MapComfyEndpoints();
    vApp.MapPhase9Endpoints();
    vApp.MapPromptChatEndpoints();

    vApp.Run();
    return 0;
}
catch (Exception vStartupException)
{
    Log.Fatal(vStartupException, "TrStudioApi terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
