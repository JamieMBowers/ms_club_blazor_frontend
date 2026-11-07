using System.Net;

namespace ClubWeb.Models;

public sealed record ApiExchange(
    string Method,
    Uri Url,
    string? RequestJson,
    HttpStatusCode? StatusCode = null,
    string? ResponseJson = null,
    Uri? Location = null,
    string? Error = null);
