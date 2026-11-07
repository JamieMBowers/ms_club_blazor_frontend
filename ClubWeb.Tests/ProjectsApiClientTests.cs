using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Text.Json;
using ClubWeb.Contracts;
using ClubWeb.Models;
using ClubWeb.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubWeb.Tests;

public sealed class ProjectsApiClientTests
{
    [Theory]
    [InlineData("[]", 0)]
    [InlineData("""[{"id":42,"name":"First"},{"id":7,"name":"Second"}]""", 2)]
    public async Task ListReadsArrayAndPreservesApiOrder(string json, int count)
    {
        using var handler = new FakeHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/api/projects", request.RequestUri!.AbsolutePath);
            return Task.FromResult(Json(HttpStatusCode.OK, json));
        });
        using var http = CreateHttpClient(handler);

        var response = await Client(http).GetAllAsync();
        var projects = response.Data;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(count, projects.Count);
        if (count > 0)
        {
            Assert.Equal(new ProjectResponse(42, "First"), projects[0]);
            Assert.Equal(new ProjectResponse(7, "Second"), projects[1]);
        }
    }

    [Fact]
    public async Task ResponseMetadataUsesActualStatusNotAnAssumed200()
    {
        using var handler = Respond(HttpStatusCode.Accepted, "[]");
        using var http = CreateHttpClient(handler);

        var client = Client(http);
        var response = await client.GetAllAsync();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(new Uri("https://workshop.test/"), client.BaseAddress);
        Assert.Null(response.Location);
        Assert.Empty(response.Data);
    }

    [Fact]
    public async Task ReadOneUsesActualId()
    {
        using var handler = new FakeHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/api/projects/73", request.RequestUri!.AbsolutePath);
            return Task.FromResult(Json(HttpStatusCode.OK, """{"id":73,"name":"Workshop"}"""));
        });
        using var http = CreateHttpClient(handler);

        var response = await Client(http).GetByIdAsync(73);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new ProjectResponse(73, "Workshop"), response.Data);
    }

    [Fact]
    public async Task CreateSendsOnlyNameAndReadsGeneratedId()
    {
        using var handler = new FakeHandler(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/projects", request.RequestUri!.AbsolutePath);
            await AssertNameOnlyAsync(request, "Campus Finder", token);
            var response = Json(HttpStatusCode.Created, """{"id":83,"name":"Campus Finder"}""");
            response.Headers.Location = new Uri("https://workshop.test/api/projects/83");
            return response;
        });
        using var http = CreateHttpClient(handler);

        var created = await Client(http).CreateAsync(new CreateProjectRequest { Name = "Campus Finder" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(new Uri("https://workshop.test/api/projects/83"), created.Location);
        Assert.Equal(83, created.Data.Id);
        Assert.Equal("Campus Finder", created.Data.Name);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task UpdateSendsOnlyNameAndAccepts204WithoutParsing()
    {
        using var handler = new FakeHandler(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("/api/projects/83", request.RequestUri!.AbsolutePath);
            await AssertNameOnlyAsync(request, "New name", token);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        using var http = CreateHttpClient(handler);

        var response = await Client(http).UpdateAsync(83, new UpdateProjectRequest { Name = "New name" });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(response.Data);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task DeleteAccepts204WithoutParsing()
    {
        using var handler = new FakeHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Delete, request.Method);
            Assert.Equal("/api/projects/83", request.RequestUri!.AbsolutePath);
            Assert.Null(request.Content);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });
        using var http = CreateHttpClient(handler);

        var response = await Client(http).DeleteAsync(83);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(response.Data);
        Assert.Equal(1, handler.CallCount);
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("name")]
    public async Task ValidationErrorsRetainStatusAndMapKnownFieldSafely(string field)
    {
        var json = $$$"""{"title":"Validation failed","errors":{"{{{field}}}":["Name is required"],"unknown":["private details"]}}""";
        using var handler = Respond(HttpStatusCode.BadRequest, json);
        using var http = CreateHttpClient(handler);

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            Client(http).CreateAsync(new CreateProjectRequest { Name = "" }));

        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        Assert.False(error.OutcomeUncertain);
        Assert.Single(error.ValidationErrors);
        Assert.Equal("Enter a nonblank project name of 100 characters or fewer.", Assert.Single(error.ValidationErrors["Name"]));
        Assert.DoesNotContain("private details", error.Message);
    }

    [Fact]
    public async Task MissingRecordIsDistinctFromAnEmptyList()
    {
        using var handler = Respond(HttpStatusCode.NotFound, "");
        using var http = CreateHttpClient(handler);

        var error = await Assert.ThrowsAsync<ApiException>(() => Client(http).GetByIdAsync(83));

        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
        Assert.Contains("no longer available", error.Message);
        Assert.False(error.OutcomeUncertain);
    }

    [Theory]
    [InlineData("<html>private SQL stack trace</html>")]
    [InlineData("")]
    [InlineData("""{"title":"private SQL stack trace","detail":"secret"}""")]
    public async Task FailureNeverDisplaysServerHtmlOrProblemDetails(string body)
    {
        using var handler = Respond(HttpStatusCode.InternalServerError, body);
        using var http = CreateHttpClient(handler);

        var error = await Assert.ThrowsAsync<ApiException>(() => Client(http).GetAllAsync());

        Assert.Equal(HttpStatusCode.InternalServerError, error.StatusCode);
        Assert.Contains("500", error.Message);
        Assert.DoesNotContain("private", error.Message);
        Assert.DoesNotContain("secret", error.Message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[null]")]
    [InlineData("""[{"id":8}]""")]
    [InlineData("""[{"id":8,"name":null}]""")]
    public async Task InvalidSuccessfulResponseIsNotSilentlyAnEmptyList(string body)
    {
        using var handler = Respond(HttpStatusCode.OK, body);
        using var http = CreateHttpClient(handler);

        var error = await Assert.ThrowsAsync<ApiException>(() => Client(http).GetAllAsync());

        Assert.Contains("unexpected response", error.Message);
        Assert.Equal(HttpStatusCode.OK, error.StatusCode);
    }

    [Fact]
    public async Task LostCreateResponseRequiresReloadAndIsNeverRetried()
    {
        using var handler = new FakeHandler((_, _) => throw new HttpRequestException("private transport details"));
        using var http = CreateHttpClient(handler);

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            Client(http).CreateAsync(new CreateProjectRequest { Name = "Workshop" }));

        Assert.True(error.OutcomeUncertain);
        Assert.Contains("Reload", error.Message);
        Assert.DoesNotContain("private", error.Message);
        Assert.Equal(1, handler.CallCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.Created, "broken JSON")]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    public async Task WriteWithUnusableResponseWarnsAboutAmbiguousOutcome(HttpStatusCode status, string body)
    {
        using var handler = Respond(status, body);
        using var http = CreateHttpClient(handler);

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            Client(http).CreateAsync(new CreateProjectRequest { Name = "Workshop" }));

        Assert.True(error.OutcomeUncertain);
        Assert.Equal(status, error.StatusCode);
        Assert.Contains("before retrying", error.Message);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task TimeoutOnWriteDoesNotClaimNothingWasSaved()
    {
        using var handler = new FakeHandler((_, _) => throw new OperationCanceledException());
        using var http = CreateHttpClient(handler);

        var error = await Assert.ThrowsAsync<ApiException>(() => Client(http).DeleteAsync(83));

        Assert.True(error.OutcomeUncertain);
        Assert.Contains("timed out", error.Message);
        Assert.Contains("may have reached", error.Message);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task CallerCancellationIsPassedThroughWithoutAnApiError()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new FakeHandler((_, token) =>
        {
            Assert.True(token.CanBeCanceled);
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(Json(HttpStatusCode.OK, "[]"));
        });
        using var http = CreateHttpClient(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(http).GetAllAsync(cancellation.Token));
    }

    [Fact]
    public async Task ResponseContentIsDisposedAfterReading()
    {
        var content = new TrackedContent("[]");
        using var handler = new FakeHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));
        using var http = CreateHttpClient(handler);

        await Client(http).GetAllAsync();

        Assert.True(content.IsDisposed);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Campus Finder", true)]
    public void FormValidatesRequiredName(string name, bool expected)
    {
        var model = new ProjectNameModel { Name = name };
        Assert.Equal(expected, Validator.TryValidateObject(model, new ValidationContext(model), [], true));
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void FormValidatesExactNameLengthBoundary(int length, bool expected)
    {
        var model = new ProjectNameModel { Name = new string('a', length) };
        Assert.Equal(expected, Validator.TryValidateObject(model, new ValidationContext(model), [], true));
    }

    private static ProjectsApiClient Client(HttpClient http) =>
        new(http, NullLogger<ProjectsApiClient>.Instance);

    private static HttpClient CreateHttpClient(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://workshop.test/") };

    private static FakeHandler Respond(HttpStatusCode status, string body) =>
        new((_, _) => Task.FromResult(Json(status, body)));

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static async Task AssertNameOnlyAsync(HttpRequestMessage request, string name, CancellationToken token)
    {
        Assert.NotNull(request.Content);
        Assert.Equal("application/json", request.Content.Headers.ContentType!.MediaType);
        using var json = JsonDocument.Parse(await request.Content.ReadAsStringAsync(token));
        var property = Assert.Single(json.RootElement.EnumerateObject());
        Assert.Equal("name", property.Name);
        Assert.Equal(name, property.Value.GetString());
    }

    private sealed class FakeHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return respond(request, cancellationToken);
        }
    }

    private sealed class TrackedContent(string body) : StringContent(body, Encoding.UTF8, "application/json")
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
