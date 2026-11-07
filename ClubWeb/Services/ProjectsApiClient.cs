using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClubWeb.Contracts;

namespace ClubWeb.Services;

public sealed class ProjectsApiClient(HttpClient httpClient, ILogger<ProjectsApiClient> logger)
{
    private const string ProjectsPath = "api/projects";
    private const string UncertainOutcome =
        " The change may have reached the API. Reload the list to check the result before retrying.";

    public Uri BaseAddress => httpClient.BaseAddress
        ?? throw new InvalidOperationException("The projects client requires an API base address.");

    // Keep actual HTTP metadata alongside the data so the workshop can show the response.
    public Task<ApiResponse<IReadOnlyList<ProjectResponse>>> GetAllAsync(CancellationToken cancellationToken = default) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Get, ProjectsPath), ReadListAsync, cancellationToken);

    public Task<ApiResponse<ProjectResponse>> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Get, $"{ProjectsPath}/{id}"), ReadProjectAsync, cancellationToken);

    public Task<ApiResponse<ProjectResponse>> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Post, ProjectsPath)
        {
            Content = JsonContent.Create(request)
        }, ReadProjectAsync, cancellationToken);

    public Task<ApiResponse<object?>> UpdateAsync(int id, UpdateProjectRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Put, $"{ProjectsPath}/{id}")
        {
            Content = JsonContent.Create(request)
        }, ReadNoContentAsync, cancellationToken);

    public Task<ApiResponse<object?>> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"{ProjectsPath}/{id}"), ReadNoContentAsync, cancellationToken);

    private async Task<ApiResponse<T>> SendAsync<T>(
        HttpRequestMessage request,
        Func<HttpResponseMessage, CancellationToken, Task<T>> readResponse,
        CancellationToken cancellationToken)
    {
        using (request)
        {
            var isWrite = request.Method != HttpMethod.Get;
            HttpStatusCode? statusCode = null;
            try
            {
                using var response = await httpClient.SendAsync(request, cancellationToken);
                statusCode = response.StatusCode;
                if (!response.IsSuccessStatusCode)
                {
                    throw await ReadApiErrorAsync(response, isWrite, cancellationToken);
                }

                var data = await readResponse(response, cancellationToken);
                return new ApiResponse<T>(data, response.StatusCode, response.Headers.Location);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Navigation cancellation belongs to the page, not an error alert.
                throw;
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Projects API {Method} timed out.", request.Method);
                throw new ApiException("The API request timed out." + (isWrite ? UncertainOutcome : " Try refreshing."),
                    statusCode, outcomeUncertain: isWrite);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning("Projects API {Method} connection failure ({Error}).", request.Method, ex.HttpRequestError);
                throw new ApiException("Could not reach the API. Check its address and HTTPS certificate."
                    + (isWrite ? UncertainOutcome : ""), statusCode, outcomeUncertain: isWrite);
            }
            catch (IOException)
            {
                logger.LogWarning("Projects API {Method} response stream failed.", request.Method);
                throw new ApiException("The API response could not be read." + (isWrite ? UncertainOutcome : " Try refreshing."),
                    statusCode, outcomeUncertain: isWrite);
            }
            catch (JsonException)
            {
                logger.LogWarning("Projects API {Method} returned invalid JSON or an unexpected response shape (HTTP {Status}).",
                    request.Method, statusCode);
                throw new ApiException("The API returned an unexpected response."
                    + (isWrite ? UncertainOutcome : " Try refreshing."), statusCode, outcomeUncertain: isWrite);
            }
        }
    }

    private async Task<ApiException> ReadApiErrorAsync(
        HttpResponseMessage response, bool isWrite, CancellationToken cancellationToken)
    {
        logger.LogWarning("Projects API returned HTTP {Status}.", (int)response.StatusCode);
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            if (response.StatusCode == HttpStatusCode.BadRequest
                && problem.ValueKind == JsonValueKind.Object
                && problem.TryGetProperty("errors", out var fields)
                && fields.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in fields.EnumerateObject())
                {
                    if (field.Name.Equals("Name", StringComparison.OrdinalIgnoreCase)
                        && field.Value.ValueKind == JsonValueKind.Array
                        && field.Value.GetArrayLength() > 0)
                    {
                        // Only the known field gets a safe local message; never echo server details/HTML.
                        errors["Name"] = ["Enter a nonblank project name of 100 characters or fewer."];
                    }
                }
            }
        }
        catch (JsonException)
        {
            logger.LogWarning("Projects API error body was empty, non-JSON, or malformed (HTTP {Status}).",
                (int)response.StatusCode);
        }

        var message = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => "The API rejected the input. Check the project name.",
            HttpStatusCode.NotFound => "This project is no longer available. Refresh the list.",
            _ => $"The API could not complete the request (HTTP {(int)response.StatusCode})."
        };
        var uncertain = isWrite && ((int)response.StatusCode >= 500
            || response.StatusCode == HttpStatusCode.RequestTimeout);
        return new ApiException(message + (uncertain ? UncertainOutcome : ""),
            response.StatusCode, errors, uncertain);
    }

    private static async Task<IReadOnlyList<ProjectResponse>> ReadListAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var projects = await response.Content.ReadFromJsonAsync<ProjectResponse[]>(cancellationToken)
            ?? throw new JsonException("Expected a project array.");
        foreach (var project in projects)
        {
            ValidateProject(project);
        }

        return projects;
    }

    private static async Task<ProjectResponse> ReadProjectAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(cancellationToken)
            ?? throw new JsonException("Expected a project.");
        ValidateProject(project);
        return project;
    }

    private static void ValidateProject(ProjectResponse? project)
    {
        if (project is null || project.Id <= 0 || string.IsNullOrWhiteSpace(project.Name))
        {
            throw new JsonException("Expected a generated ID and a project name.");
        }
    }

    private static Task<object?> ReadNoContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        // PUT and DELETE return 204: there is deliberately no JSON to read.
        if (response.StatusCode != HttpStatusCode.NoContent)
        {
            throw new JsonException("Expected 204 No Content.");
        }

        return Task.FromResult<object?>(null);
    }
}
