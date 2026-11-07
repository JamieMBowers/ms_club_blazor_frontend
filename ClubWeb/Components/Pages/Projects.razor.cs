using System.Net;
using System.Text.Json;
using ClubWeb.Contracts;
using ClubWeb.Models;
using ClubWeb.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace ClubWeb.Components.Pages;

public partial class Projects
{
    private enum ListState { Loading, Loaded, Failed }

    private static readonly JsonSerializerOptions DisplayJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly CancellationTokenSource lifetime = new();
    private readonly List<ApiExchange> activity = [];
    private IReadOnlyList<ProjectResponse> projects = [];
    private ProjectNameModel createModel = new();
    private EditContext createContext;
    private ValidationMessageStore createMessages;
    private ProjectNameModel? editModel;
    private EditContext? editContext;
    private ValidationMessageStore? editMessages;
    private int? editingId;
    private ProjectResponse? selectedProject;
    private ProjectResponse? pendingDelete;
    private ApiExchange? selectedExchange;
    private ListState listState = ListState.Loading;
    private string search = "";
    private string busyMessage = "Loading your projects...";
    private string? successMessage;
    private string? errorMessage;
    private bool busy = true;
    private bool requestInFlight;
    private bool disposed;
    private bool refreshRequired;

    private IEnumerable<ProjectResponse> FilteredProjects => projects.Where(project =>
        project.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
        || project.Id.ToString().Contains(search.Trim(), StringComparison.Ordinal));

    private string ConnectionText => listState switch
    {
        ListState.Loading => "Loading from your API",
        ListState.Loaded => "Projects loaded from your API",
        _ => "Projects could not be refreshed"
    };

    public Projects()
    {
        createContext = new EditContext(createModel);
        createMessages = new ValidationMessageStore(createContext);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        // This runs only after the interactive circuit starts, not during prerendering.
        busy = false;
        await LoadAsync();
        if (!disposed) StateHasChanged();
    }

    private async Task LoadAsync()
    {
        if (busy) return;
        busy = true;
        busyMessage = "Refreshing your projects...";
        errorMessage = null;
        try
        {
            await RefreshListAsync(afterWrite: false);
        }
        catch (OperationCanceledException) when (disposed)
        {
        }
        finally
        {
            busy = false;
        }
    }

    private async Task RefreshListAsync(bool afterWrite)
    {
        listState = ListState.Loading;
        try
        {
            projects = await RequestAsync("GET", "api/projects", null, () => Api.GetAllAsync(lifetime.Token));
            listState = ListState.Loaded;
            refreshRequired = false;
            if (selectedProject is not null)
            {
                selectedProject = projects.FirstOrDefault(project => project.Id == selectedProject.Id);
            }
        }
        catch (ApiException ex)
        {
            listState = ListState.Failed;
            errorMessage = afterWrite
                ? $"Your change was saved, but the project list could not refresh. {ex.Message}"
                : ex.Message;
        }
    }

    private async Task CreateAsync()
    {
        if (busy || refreshRequired || editModel is not null || pendingDelete is not null) return;
        if (!Validate(createContext, createMessages)) return;
        var body = new CreateProjectRequest { Name = createModel.Name };
        await MutateAsync(async () =>
        {
            var created = await RequestAsync("POST", "api/projects", JsonSerializer.Serialize(body, DisplayJson),
                () => Api.CreateAsync(body, lifetime.Token));
            createModel = new ProjectNameModel();
            createContext = new EditContext(createModel);
            createMessages = new ValidationMessageStore(createContext);
            search = "";
            return $"Added {created.Name} (ID {created.Id}).";
        }, createContext, createMessages);
    }

    private async Task ViewAsync(ProjectResponse project)
    {
        if (busy || editModel is not null || pendingDelete is not null) return;
        busy = true;
        busyMessage = "Loading project details...";
        errorMessage = null;
        selectedProject = null;
        try
        {
            selectedProject = await RequestAsync("GET", $"api/projects/{project.Id}", null,
                () => Api.GetByIdAsync(project.Id, lifetime.Token));
        }
        catch (ApiException ex)
        {
            errorMessage = ex.Message;
        }
        catch (OperationCanceledException) when (disposed)
        {
        }
        finally
        {
            busy = false;
        }
    }

    private void BeginEdit(ProjectResponse project)
    {
        if (busy || refreshRequired || editModel is not null || pendingDelete is not null) return;
        errorMessage = null;
        successMessage = null;
        editingId = project.Id;
        editModel = new ProjectNameModel { Name = project.Name };
        editContext = new EditContext(editModel);
        editMessages = new ValidationMessageStore(editContext);
    }

    private void ClearEdit()
    {
        editingId = null;
        editModel = null;
        editContext = null;
        editMessages = null;
    }

    private void CancelEdit()
    {
        if (busy) return;
        ClearEdit();
    }

    private async Task SaveAsync()
    {
        if (busy || refreshRequired || editingId is not int id
            || editModel is null || editContext is null || editMessages is null) return;
        if (!Validate(editContext, editMessages)) return;
        var body = new UpdateProjectRequest { Name = editModel.Name };
        await MutateAsync(async () =>
        {
            await RequestAsync("PUT", $"api/projects/{id}", JsonSerializer.Serialize(body, DisplayJson),
                () => Api.UpdateAsync(id, body, lifetime.Token));
            ClearEdit();
            return $"Renamed project #{id}.";
        }, editContext, editMessages);
    }

    private void BeginDelete(ProjectResponse project)
    {
        if (busy || refreshRequired || editModel is not null || pendingDelete is not null) return;
        errorMessage = null;
        successMessage = null;
        pendingDelete = project;
    }

    private void CancelDelete()
    {
        if (busy) return;
        pendingDelete = null;
    }

    private async Task ConfirmDeleteAsync()
    {
        if (busy || refreshRequired || pendingDelete is null) return;
        var project = pendingDelete;
        await MutateAsync(async () =>
        {
            await RequestAsync("DELETE", $"api/projects/{project.Id}", null,
                () => Api.DeleteAsync(project.Id, lifetime.Token));
            pendingDelete = null;
            if (selectedProject?.Id == project.Id) selectedProject = null;
            return $"Deleted {project.Name} (ID {project.Id}).";
        });
    }

    private bool Validate(EditContext context, ValidationMessageStore messages)
    {
        errorMessage = null;
        successMessage = null;
        messages.Clear();
        context.NotifyValidationStateChanged();
        return context.Validate();
    }

    private async Task MutateAsync(
        Func<Task<string>> write, EditContext? context = null, ValidationMessageStore? messages = null)
    {
        if (busy) return;
        busy = true;
        busyMessage = "Saving your change...";
        errorMessage = null;
        successMessage = null;
        try
        {
            successMessage = await write();
            busyMessage = "Refreshing your projects...";
            StateHasChanged();
            // A successful write and a failed refresh have separate outcomes.
            await RefreshListAsync(afterWrite: true);
        }
        catch (ApiException ex)
        {
            errorMessage = ex.Message;
            refreshRequired |= ex.OutcomeUncertain;
            if (context is not null && messages is not null)
            {
                foreach (var field in ex.ValidationErrors)
                {
                    if (field.Key.Equals(nameof(ProjectNameModel.Name), StringComparison.OrdinalIgnoreCase))
                    {
                        messages.Add(context.Field(nameof(ProjectNameModel.Name)), field.Value);
                    }
                }
                context.NotifyValidationStateChanged();
            }
        }
        catch (OperationCanceledException) when (disposed)
        {
        }
        finally
        {
            busy = false;
        }
    }

    private async Task<T> RequestAsync<T>(
        string method, string path, string? json, Func<Task<ApiResponse<T>>> send)
    {
        var sent = new ApiExchange(method, new Uri(Api.BaseAddress, path), json);
        selectedExchange = sent;
        requestInFlight = true;
        try
        {
            var response = await send();
            RecordExchange(sent with
            {
                StatusCode = response.StatusCode,
                Location = response.Location,
                ResponseJson = response.StatusCode == HttpStatusCode.NoContent
                    ? null : JsonSerializer.Serialize(response.Data, DisplayJson)
            });
            return response.Data;
        }
        catch (ApiException ex)
        {
            RecordExchange(sent with
            {
                StatusCode = ex.StatusCode,
                Error = ex.Message,
                ResponseJson = JsonSerializer.Serialize(new
                {
                    status = ex.StatusCode is { } status ? (int?)status : null,
                    title = ex.Message,
                    errors = ex.ValidationErrors
                }, DisplayJson)
            });
            throw;
        }
        finally
        {
            requestInFlight = false;
        }
    }

    private void RecordExchange(ApiExchange exchange)
    {
        selectedExchange = exchange;
        activity.Insert(0, exchange);
        if (activity.Count > 6) activity.RemoveAt(activity.Count - 1);
    }

    public void Dispose()
    {
        disposed = true;
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
