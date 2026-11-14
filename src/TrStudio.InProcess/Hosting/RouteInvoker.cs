using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace TrStudio.InProcess.Hosting;

/// <summary>
/// Binds a route handler's parameters the way ASP.NET Core minimal APIs did (phase 3): the
/// cancellation token, the signed-in user, the request context, path values, query values, services
/// and the JSON body; then runs it and returns its <see cref="IResult"/>.
/// </summary>
internal static class RouteInvoker
{
    public static async Task<IResult> InvokeAsync(
        RouteEntry aRoute,
        IServiceProvider aScope,
        HttpContext aContext,
        IReadOnlyDictionary<string, string> aRouteValues,
        IReadOnlyDictionary<string, string> aQuery,
        string? aBody,
        JsonSerializerOptions aJson,
        CancellationToken aCancellationToken)
    {
        var vMethod = aRoute.Handler.Method;
        var vParameters = vMethod.GetParameters();
        var vArguments = new object?[vParameters.Length];
        var vIsService = aScope.GetService<IServiceProviderIsService>();

        for (var vIndex = 0; vIndex < vParameters.Length; vIndex++)
        {
            vArguments[vIndex] = Bind(vParameters[vIndex], aScope, vIsService, aContext, aRouteValues, aQuery, aBody, aJson, aCancellationToken);
        }

        object? vReturned;
        try
        {
            vReturned = aRoute.Handler.Method.Invoke(aRoute.Handler.Target, vArguments);
        }
        catch (TargetInvocationException vException) when (vException.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(vException.InnerException).Throw();
            throw;
        }

        switch (vReturned)
        {
            case IResult vResult:
                return vResult;
            case Task vTask:
                await vTask.ConfigureAwait(false);
                var vValue = vTask.GetType().GetProperty("Result")?.GetValue(vTask);
                return vValue as IResult ?? (vValue is null || vValue.GetType().Name == "VoidTaskResult" ? Results.Ok() : Results.Ok(vValue));
            case null:
                return Results.Ok();
            default:
                return Results.Ok(vReturned);
        }
    }

    private static object? Bind(
        ParameterInfo aParameter,
        IServiceProvider aScope,
        IServiceProviderIsService? aIsService,
        HttpContext aContext,
        IReadOnlyDictionary<string, string> aRouteValues,
        IReadOnlyDictionary<string, string> aQuery,
        string? aBody,
        JsonSerializerOptions aJson,
        CancellationToken aCancellationToken)
    {
        var vType = aParameter.ParameterType;
        if (vType == typeof(CancellationToken))
        {
            return aCancellationToken;
        }

        if (vType == typeof(ClaimsPrincipal))
        {
            return aContext.User;
        }

        if (vType == typeof(HttpContext))
        {
            return aContext;
        }

        var vName = aParameter.Name ?? string.Empty;
        if (aRouteValues.TryGetValue(vName, out var vRouteValue))
        {
            return Convert(vRouteValue, vType);
        }

        var vFromQuery = aParameter.GetCustomAttribute<FromQueryAttribute>();
        if (vFromQuery is not null)
        {
            return aQuery.TryGetValue(vFromQuery.Name ?? vName, out var vNamed) ? Convert(vNamed, vType) : DefaultOf(aParameter);
        }

        if (IsSimple(vType))
        {
            return TryQuery(aQuery, vName, out var vQueryValue) ? Convert(vQueryValue, vType) : DefaultOf(aParameter);
        }

        if (aIsService?.IsService(vType) == true)
        {
            return aScope.GetRequiredService(vType);
        }

        if (string.IsNullOrWhiteSpace(aBody))
        {
            return DefaultOf(aParameter);
        }

        return JsonSerializer.Deserialize(aBody, vType, aJson);
    }

    private static bool TryQuery(IReadOnlyDictionary<string, string> aQuery, string aName, out string aValue)
    {
        if (aQuery.TryGetValue(aName, out aValue!))
        {
            return true;
        }

        // The screens send "page", the handlers name it "aPage".
        if (aName.Length > 1 && aName[0] == 'a' && char.IsUpper(aName[1]) && aQuery.TryGetValue(aName[1..], out aValue!))
        {
            return true;
        }

        aValue = string.Empty;
        return false;
    }

    private static object? DefaultOf(ParameterInfo aParameter)
    {
        if (aParameter.HasDefaultValue)
        {
            return aParameter.DefaultValue;
        }

        return aParameter.ParameterType.IsValueType ? Activator.CreateInstance(aParameter.ParameterType) : null;
    }

    private static bool IsSimple(Type aType)
    {
        var vType = Nullable.GetUnderlyingType(aType) ?? aType;
        return vType.IsPrimitive || vType.IsEnum || vType == typeof(string) || vType == typeof(Guid)
            || vType == typeof(decimal) || vType == typeof(DateTime) || vType == typeof(DateTimeOffset);
    }

    private static object? Convert(string aValue, Type aType)
    {
        var vType = Nullable.GetUnderlyingType(aType) ?? aType;
        if (string.IsNullOrEmpty(aValue) && vType != typeof(string))
        {
            return Nullable.GetUnderlyingType(aType) is not null || !aType.IsValueType ? null : Activator.CreateInstance(aType);
        }

        if (vType == typeof(string))
        {
            return aValue;
        }

        if (vType == typeof(Guid))
        {
            return Guid.Parse(aValue);
        }

        if (vType.IsEnum)
        {
            return Enum.Parse(vType, aValue, ignoreCase: true);
        }

        if (vType == typeof(DateTime))
        {
            return DateTime.Parse(aValue, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        }

        if (vType == typeof(DateTimeOffset))
        {
            return DateTimeOffset.Parse(aValue, CultureInfo.InvariantCulture);
        }

        return System.Convert.ChangeType(aValue, vType, CultureInfo.InvariantCulture);
    }
}
