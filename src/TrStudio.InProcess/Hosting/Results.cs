using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TrStudio.InProcess.Hosting;

/// <summary>
/// The answer of an in-process route (phase 3, ADR-015). Mirrors the shape of ASP.NET Core's
/// <c>IResult</c> so the route files read like the API's endpoint files they were ported from.
/// </summary>
public interface IResult
{
    /// <summary>Turns the result into the HTTP response the screens' HttpClient receives.</summary>
    /// <param name="aJson">JSON options (web defaults: camelCase names).</param>
    /// <returns>The response.</returns>
    HttpResponseMessage ToResponse(JsonSerializerOptions aJson);
}

/// <summary>The HTTP status codes the routes use (same names as ASP.NET Core's).</summary>
public static class StatusCodes
{
    public const int Status200OK = 200;
    public const int Status201Created = 201;
    public const int Status202Accepted = 202;
    public const int Status204NoContent = 204;
    public const int Status400BadRequest = 400;
    public const int Status401Unauthorized = 401;
    public const int Status402PaymentRequired = 402;
    public const int Status403Forbidden = 403;
    public const int Status404NotFound = 404;
    public const int Status409Conflict = 409;
    public const int Status413PayloadTooLarge = 413;
    public const int Status422UnprocessableEntity = 422;
    public const int Status429TooManyRequests = 429;
    public const int Status500InternalServerError = 500;
    public const int Status502BadGateway = 502;
    public const int Status503ServiceUnavailable = 503;
}

/// <summary>Factory of route results, named as in ASP.NET Core's <c>Results</c>.</summary>
public static class Results
{
    /// <summary>200 with an optional JSON body.</summary>
    public static IResult Ok(object? aValue = null) => new JsonResult(200, aValue, aValue is not null);

    /// <summary>201 with a Location header and a JSON body.</summary>
    public static IResult Created(string? aUri, object? aValue) => new JsonResult(201, aValue, aValue is not null, aUri);

    /// <summary>202 with an optional JSON body.</summary>
    public static IResult Accepted(string? aUri = null, object? aValue = null) => new JsonResult(202, aValue, aValue is not null, aUri);

    /// <summary>204.</summary>
    public static IResult NoContent() => new JsonResult(204, null, false);

    /// <summary>400 with an optional JSON body.</summary>
    public static IResult BadRequest(object? aError = null) => new JsonResult(400, aError, aError is not null);

    /// <summary>401.</summary>
    public static IResult Unauthorized() => new JsonResult(401, null, false);

    /// <summary>403.</summary>
    public static IResult Forbid() => new JsonResult(403, null, false);

    /// <summary>404 with an optional JSON body.</summary>
    public static IResult NotFound(object? aValue = null) => new JsonResult(404, aValue, aValue is not null);

    /// <summary>409 with an optional JSON body.</summary>
    public static IResult Conflict(object? aError = null) => new JsonResult(409, aError, aError is not null);

    /// <summary>422 with an optional JSON body.</summary>
    public static IResult UnprocessableEntity(object? aError = null) => new JsonResult(422, aError, aError is not null);

    /// <summary>A bare status code.</summary>
    public static IResult StatusCode(int aStatusCode) => new JsonResult(aStatusCode, null, false);

    /// <summary>A JSON body with a chosen status code.</summary>
    public static IResult Json(object? aData, JsonSerializerOptions? aOptions = null, string? aContentType = null, int? statusCode = null)
        => new JsonResult(statusCode ?? 200, aData, true);

    /// <summary>A problem-details body (<c>application/problem+json</c>).</summary>
    public static IResult Problem(
        string? detail = null,
        string? instance = null,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        IDictionary<string, object?>? extensions = null)
        => new ProblemResult(statusCode ?? 500, title, detail, type, extensions);

    /// <summary>A file download.</summary>
    public static IResult File(byte[] aContents, string? contentType = null, string? fileDownloadName = null)
        => new FileResult(aContents, contentType ?? "application/octet-stream", fileDownloadName);

    private sealed class JsonResult : IResult
    {
        private readonly int objStatus;
        private readonly object? objValue;
        private readonly bool objHasBody;
        private readonly string? objLocation;

        public JsonResult(int aStatus, object? aValue, bool aHasBody, string? aLocation = null)
        {
            objStatus = aStatus;
            objValue = aValue;
            objHasBody = aHasBody;
            objLocation = aLocation;
        }

        public HttpResponseMessage ToResponse(JsonSerializerOptions aJson)
        {
            var vResponse = new HttpResponseMessage((HttpStatusCode)objStatus);
            if (objHasBody)
            {
                var vText = JsonSerializer.Serialize(objValue, objValue?.GetType() ?? typeof(object), aJson);
                vResponse.Content = new StringContent(vText, Encoding.UTF8, "application/json");
            }

            if (!string.IsNullOrEmpty(objLocation))
            {
                vResponse.Headers.Location = new Uri(objLocation, UriKind.RelativeOrAbsolute);
            }

            return vResponse;
        }
    }

    private sealed class ProblemResult : IResult
    {
        private readonly Dictionary<string, object?> objBody = new();
        private readonly int objStatus;

        public ProblemResult(int aStatus, string? aTitle, string? aDetail, string? aType, IDictionary<string, object?>? aExtensions)
        {
            objStatus = aStatus;
            objBody["type"] = aType ?? "about:blank";
            objBody["title"] = aTitle;
            objBody["status"] = aStatus;
            if (aDetail is not null)
            {
                objBody["detail"] = aDetail;
            }

            if (aExtensions is not null)
            {
                foreach (var vPair in aExtensions)
                {
                    objBody[vPair.Key] = vPair.Value;
                }
            }
        }

        public HttpResponseMessage ToResponse(JsonSerializerOptions aJson)
        {
            var vResponse = new HttpResponseMessage((HttpStatusCode)objStatus)
            {
                Content = new StringContent(JsonSerializer.Serialize(objBody, aJson), Encoding.UTF8)
            };
            vResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("application/problem+json") { CharSet = "utf-8" };
            return vResponse;
        }
    }

    private sealed class FileResult : IResult
    {
        private readonly byte[] objContents;
        private readonly string objContentType;
        private readonly string? objFileName;

        public FileResult(byte[] aContents, string aContentType, string? aFileName)
        {
            objContents = aContents;
            objContentType = aContentType;
            objFileName = aFileName;
        }

        public HttpResponseMessage ToResponse(JsonSerializerOptions aJson)
        {
            var vResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(objContents) };
            vResponse.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(objContentType);
            if (!string.IsNullOrEmpty(objFileName))
            {
                vResponse.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = objFileName };
            }

            return vResponse;
        }
    }
}
