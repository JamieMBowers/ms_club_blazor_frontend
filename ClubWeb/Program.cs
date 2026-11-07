using ClubWeb.Components;
using ClubWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var configuredUrl = builder.Configuration["ClubApi:BaseUrl"];
if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var apiBaseUrl)
    || (apiBaseUrl.Scheme != Uri.UriSchemeHttps && apiBaseUrl.Scheme != Uri.UriSchemeHttp)
    || !string.IsNullOrEmpty(apiBaseUrl.UserInfo)
    || !string.IsNullOrEmpty(apiBaseUrl.Query)
    || !string.IsNullOrEmpty(apiBaseUrl.Fragment)
    || !apiBaseUrl.AbsolutePath.EndsWith('/'))
{
    throw new InvalidOperationException(
        "Set ClubApi:BaseUrl to an absolute HTTP(S) URL ending in '/', without credentials, a query, or a fragment.");
}

// DI gives components a typed client; these HTTP requests run on the Blazor server.
builder.Services.AddHttpClient<ProjectsApiClient>(client =>
{
    client.BaseAddress = apiBaseUrl;
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
