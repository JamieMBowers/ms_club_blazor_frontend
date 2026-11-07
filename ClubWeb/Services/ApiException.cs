using System.Net;

namespace ClubWeb.Services;

public sealed class ApiException(
    string message,
    HttpStatusCode? statusCode = null,
    IReadOnlyDictionary<string, string[]>? validationErrors = null,
    bool outcomeUncertain = false) : Exception(message)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; } =
        validationErrors ?? new Dictionary<string, string[]>();
    public bool OutcomeUncertain { get; } = outcomeUncertain;
}
