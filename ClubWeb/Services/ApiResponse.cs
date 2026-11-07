using System.Net;

namespace ClubWeb.Services;

public sealed record ApiResponse<T>(T Data, HttpStatusCode StatusCode, Uri? Location);
