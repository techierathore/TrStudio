using System.Security.Claims;
using TrStudio.Models;
using TrStudio.Models.Contracts;
using TrStudio.Models.Entities;
using TrStudio.Models.Repositories;
using TrStudio.Models.Services;

namespace TrStudioApi.Endpoints;

/// <summary>
/// Endpoint mapping for the Prompt Studio chat cluster (<c>/api/v1/prompt-chat</c>, FN-073 / BRD-91):
/// persisted, per-user chat sessions and messages plus the message→reply path (which persists the
/// user turn, invokes <see cref="IPromptChatService"/> for the completion, and persists the assistant
/// reply). EVERY handler resolves the caller's AppManager user id and passes it into the owner-filtered
/// repository so a user can never read or mutate another user's chats (BRD-81) — a foreign session
/// reads as 404. The global <c>FallbackPolicy</c> requires an authenticated user on every route.
/// </summary>
/// <remarks>
/// OWNED BY: the Phase-10 prompt-chat persistence cluster (FN-073). Only that cluster edits this file.
/// </remarks>
public static class PromptChatEndpoints
{
    private const string DefaultSessionTitle = "New chat";

    /// <summary>
    /// Maps the prompt-chat endpoints onto the application pipeline.
    /// </summary>
    /// <param name="aApp">The web application to map endpoints onto.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication MapPromptChatEndpoints(this WebApplication aApp)
    {
        var vGroup = aApp.MapGroup(ApiRoutes.PromptChat).WithTags("PromptChat");
        vGroup.MapPost("sessions", CreateSessionAsync).WithName("CreatePromptChatSession");
        vGroup.MapGet("sessions", ListSessionsAsync).WithName("ListPromptChatSessions");
        vGroup.MapGet("sessions/{aSessionId:guid}", GetSessionAsync).WithName("GetPromptChatSession");
        vGroup.MapPut("sessions/{aSessionId:guid}", RenameSessionAsync).WithName("RenamePromptChatSession");
        vGroup.MapDelete("sessions/{aSessionId:guid}", DeleteSessionAsync).WithName("DeletePromptChatSession");
        vGroup.MapPost("sessions/{aSessionId:guid}/messages", PostMessageAsync).WithName("PostPromptChatMessage");

        return aApp;
    }

    private static async Task<IResult> CreateSessionAsync(
        PromptChatSessionCreateRequest aRequest,
        ClaimsPrincipal aUser,
        IPromptChatRepository aRepository,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(aRequest.ModelId))
        {
            return Results.BadRequest(new { error = "ModelId is required." });
        }

        var vNow = DateTime.UtcNow;
        var vSession = new PromptChatSession
        {
            SessionId = Guid.NewGuid(),
            UserId = vUserId,
            Title = string.IsNullOrWhiteSpace(aRequest.Title) ? DefaultSessionTitle : aRequest.Title.Trim(),
            ModelId = aRequest.ModelId.Trim(),
            CreatedUtc = vNow,
            UpdatedUtc = vNow
        };

        await aRepository.CreateSessionAsync(vSession, aCancellationToken);
        return Results.Created($"{ApiRoutes.PromptChat}/sessions/{vSession.SessionId}", ToSessionResponse(vSession));
    }

    private static async Task<IResult> ListSessionsAsync(
        ClaimsPrincipal aUser,
        IPromptChatRepository aRepository,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vSessions = await aRepository.ListSessionsAsync(vUserId, aCancellationToken);
        return Results.Ok(vSessions.Select(ToSessionResponse).ToList());
    }

    private static async Task<IResult> GetSessionAsync(
        Guid aSessionId,
        ClaimsPrincipal aUser,
        IPromptChatRepository aRepository,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        // Owner-filtered read: a foreign or missing session is indistinguishable — never leak existence.
        var vSession = await aRepository.GetSessionAsync(aSessionId, vUserId, aCancellationToken);
        if (vSession is null)
        {
            return Results.NotFound();
        }

        var vMessages = await aRepository.ListMessagesAsync(aSessionId, vUserId, aCancellationToken);
        return Results.Ok(new PromptChatSessionDetailResponse
        {
            Session = ToSessionResponse(vSession),
            Messages = vMessages.Select(ToMessageResponse).ToList()
        });
    }

    private static async Task<IResult> RenameSessionAsync(
        Guid aSessionId,
        PromptChatSessionRenameRequest aRequest,
        ClaimsPrincipal aUser,
        IPromptChatRepository aRepository,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(aRequest.Title))
        {
            return Results.BadRequest(new { error = "Title is required." });
        }

        // Owner-filtered read first: a foreign session must read as 404, never as a silent no-op.
        var vSession = await aRepository.GetSessionAsync(aSessionId, vUserId, aCancellationToken);
        if (vSession is null)
        {
            return Results.NotFound();
        }

        var vTitle = aRequest.Title.Trim();
        await aRepository.RenameSessionAsync(aSessionId, vUserId, vTitle, aCancellationToken);
        vSession.Title = vTitle;
        vSession.UpdatedUtc = DateTime.UtcNow;
        return Results.Ok(ToSessionResponse(vSession));
    }

    private static async Task<IResult> DeleteSessionAsync(
        Guid aSessionId,
        ClaimsPrincipal aUser,
        IPromptChatRepository aRepository,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        var vSession = await aRepository.GetSessionAsync(aSessionId, vUserId, aCancellationToken);
        if (vSession is null)
        {
            return Results.NotFound();
        }

        // Removes the session and all of its messages, both owner-filtered (BRD-81).
        await aRepository.DeleteSessionAsync(aSessionId, vUserId, aCancellationToken);
        return Results.Ok(new { deleted = true });
    }

    private static async Task<IResult> PostMessageAsync(
        Guid aSessionId,
        PromptChatPostMessageRequest aRequest,
        ClaimsPrincipal aUser,
        IPromptChatRepository aRepository,
        IPromptChatService aChatService,
        CancellationToken aCancellationToken)
    {
        var vUserId = ResolveUserId(aUser);
        if (vUserId is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(aRequest.Message))
        {
            return Results.BadRequest(new { error = "Message is required." });
        }

        // Owner-filtered load: a foreign or missing session reads as 404 (BRD-81).
        var vSession = await aRepository.GetSessionAsync(aSessionId, vUserId, aCancellationToken);
        if (vSession is null)
        {
            return Results.NotFound();
        }

        // Decode the optional attached image up-front so a malformed payload fails before any write.
        byte[]? vImageBytes = null;
        if (!string.IsNullOrWhiteSpace(aRequest.ImageBase64))
        {
            try
            {
                vImageBytes = Convert.FromBase64String(aRequest.ImageBase64);
            }
            catch (FormatException)
            {
                return Results.BadRequest(new { error = "ImageBase64 is not valid base64." });
            }
        }

        // Prior turns (oldest first) become the completion history; captured BEFORE persisting the
        // new user turn so the new message is not duplicated into its own history.
        var vPriorMessages = await aRepository.ListMessagesAsync(aSessionId, vUserId, aCancellationToken);
        var vTurns = vPriorMessages
            .Select(aMessage => new PromptChatTurn { Role = aMessage.Role, Content = aMessage.Content })
            .ToList();

        var vNow = DateTime.UtcNow;
        var vUserMessage = new PromptChatMessage
        {
            MessageId = Guid.NewGuid(),
            SessionId = aSessionId,
            UserId = vUserId,
            Role = "user",
            Content = aRequest.Message,
            HasImage = vImageBytes is not null,
            CreatedUtc = vNow
        };
        await aRepository.AddMessageAsync(vUserMessage, aCancellationToken);

        var vResult = await aChatService.CompleteAsync(new PromptChatCompletionRequest
        {
            ModelId = vSession.ModelId,
            History = vTurns,
            Message = aRequest.Message,
            Image = vImageBytes,
            ImageMediaType = aRequest.ImageMediaType
        }, vUserId, aCancellationToken);

        if (vResult.Rejected)
        {
            // Refused before any reply: do NOT persist an assistant message; surface the reason (400).
            return Results.BadRequest(new PromptChatPostMessageResponse
            {
                UserMessage = ToMessageResponse(vUserMessage),
                AssistantMessage = null,
                Rejected = true,
                RejectionReason = vResult.RejectionReason,
                Degraded = vResult.Degraded
            });
        }

        var vAssistantMessage = new PromptChatMessage
        {
            MessageId = Guid.NewGuid(),
            SessionId = aSessionId,
            UserId = vUserId,
            Role = "assistant",
            Content = vResult.Content,
            HasImage = false,
            CreatedUtc = DateTime.UtcNow
        };
        await aRepository.AddMessageAsync(vAssistantMessage, aCancellationToken);
        await aRepository.TouchSessionAsync(aSessionId, vUserId, aCancellationToken);

        return Results.Ok(new PromptChatPostMessageResponse
        {
            UserMessage = ToMessageResponse(vUserMessage),
            AssistantMessage = ToMessageResponse(vAssistantMessage),
            Rejected = false,
            RejectionReason = null,
            Degraded = vResult.Degraded
        });
    }

    private static PromptChatSessionResponse ToSessionResponse(PromptChatSession aSession) => new()
    {
        SessionId = aSession.SessionId,
        Title = aSession.Title,
        ModelId = aSession.ModelId,
        UpdatedUtc = aSession.UpdatedUtc
    };

    private static PromptChatMessageResponse ToMessageResponse(PromptChatMessage aMessage) => new()
    {
        MessageId = aMessage.MessageId,
        Role = aMessage.Role,
        Content = aMessage.Content,
        HasImage = aMessage.HasImage,
        CreatedUtc = aMessage.CreatedUtc
    };

    private static string? ResolveUserId(ClaimsPrincipal aUser)
        => aUser.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? aUser.FindFirst("sub")?.Value;
}
