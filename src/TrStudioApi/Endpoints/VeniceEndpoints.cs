using TrStudio.Engine.DependencyInjection;
using TrStudio.Engine.Domain.Venice;
using TrStudio.Models;
using TrStudio.Models.Contracts;
using TrStudio.Models.Services;

namespace TrStudioApi.Endpoints;

/// <summary>
/// Phase 2 Venice admin endpoints (ADR-014): Test connection for a stored provider (BRD-95) and
/// "Fill from Venice" (decision 4B, BRD-122), which reads Venice's public model list and saves
/// nothing. Both read Venice's address (and key) from the database only.
/// </summary>
public static class VeniceEndpoints
{
    private static readonly string[] ListTypes = { "image", "inpaint", "upscale", "video" };

    /// <summary>Maps the Venice admin endpoints.</summary>
    /// <param name="aApp">The web application.</param>
    /// <returns>The same application for chaining.</returns>
    public static WebApplication MapVeniceEndpoints(this WebApplication aApp)
    {
        aApp.MapPost(ApiRoutes.Providers + "/keys/{aProviderName}/test", TestConnectionAsync)
            .RequireAuthorization(SpineModule.AdminOnlyPolicy)
            .WithTags("AdminProviders")
            .WithName("TestProviderConnection");

        aApp.MapPost(ApiRoutes.VeniceFill, FillAsync)
            .RequireAuthorization(SpineModule.AdminOnlyPolicy)
            .WithTags("AdminRegistry")
            .WithName("FillFromVenice");

        return aApp;
    }

    private static async Task<IResult> TestConnectionAsync(
        string aProviderName,
        IVeniceConnectionReader aReader,
        VeniceClient aClient,
        CancellationToken aCancellationToken)
    {
        if (!string.Equals(aProviderName, VeniceConnectionReader.ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = "Test connection is available for Venice only." });
        }

        var vResponse = new ProviderTestConnectionResponse { ProviderName = VeniceConnectionReader.ProviderName };
        try
        {
            var vConnection = await aReader.ReadAsync(aCancellationToken);
            await aClient.ListModelsAsync(vConnection.BaseUrl, "image", vConnection.ApiKey, aCancellationToken);
            vResponse.Connected = true;
            vResponse.HttpStatus = 200;
            vResponse.Message = "Connected";
        }
        catch (VeniceApiException vException)
        {
            vResponse.HttpStatus = vException.StatusCode;
            vResponse.Message = vException.Message;
        }
        catch (InvalidOperationException vException)
        {
            vResponse.Message = vException.Message;
        }

        return Results.Ok(vResponse);
    }

    private static async Task<IResult> FillAsync(
        VeniceFillRequest aRequest,
        IVeniceConnectionReader aReader,
        VeniceClient aClient,
        CancellationToken aCancellationToken)
    {
        if (string.IsNullOrWhiteSpace(aRequest.ProviderModelId))
        {
            return Results.BadRequest(new { error = "ProviderModelId is required." });
        }

        var vBaseUrl = await aReader.ReadBaseUrlAsync(aCancellationToken);
        if (vBaseUrl is null)
        {
            return Results.BadRequest(new { error = VeniceConnectionReader.NotConfiguredMessage });
        }

        try
        {
            foreach (var vType in ListTypes)
            {
                var vList = await aClient.ListModelsAsync(vBaseUrl, vType, null, aCancellationToken);
                var vEntry = vList.FirstOrDefault(aEntry => string.Equals(aEntry.Id, aRequest.ProviderModelId.Trim(), StringComparison.OrdinalIgnoreCase));
                if (vEntry is null)
                {
                    continue;
                }

                var vFill = vEntry.ToFill();
                return vFill is null
                    ? Results.UnprocessableEntity(new { error = $"Venice model '{vEntry.Id}' is a kind TrStudio does not send to Venice." })
                    : Results.Ok(vFill);
            }
        }
        catch (VeniceApiException vException)
        {
            return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "PROVIDER_FAILED", detail: vException.Message);
        }

        return Results.NotFound(new { error = $"Venice's model list has no model '{aRequest.ProviderModelId.Trim()}'." });
    }
}
