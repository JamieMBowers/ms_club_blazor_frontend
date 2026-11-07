# ClubWeb: club projects

A .NET 10 **Blazor Interactive Server** frontend for the existing ClubApi.
Use forms and a searchable project list, rather than choosing endpoints.
The frontend has no database connection, SQL packages, mock records, backend
project reference, or authentication screen.

This is the standalone frontend repository. The backend lives in
[ms-club-api](https://github.com/JamieMBowers/ms-club-api).

**First time running the workshop? Start with the [student setup guide](../README.md).**
It walks through installing tools, cloning both repositories, preparing Azure
SQL, and opening the app in your browser. This file explains the UI and code
in more detail. All commands below run from the frontend repository root.

## What the app does

- Loads existing projects automatically once the interactive page connects.
- Adds a project with a validated name form.
- Searches loaded projects by name or ID without another API request.
- Views a project's current details, renames it, and confirms deletion.
- Refreshes the list after successful writes.
- Shows useful loading, empty, validation, success, and failure states.
- Keeps the last six HTTP exchanges in an optional **Behind the scenes: API
  activity** section. Select an entry to see its real status, formatted JSON,
  request body, and Location header. This history is in memory for the current
  page only.

Only ID and name exist in the backend contract. The ID is assigned by the API,
not entered in the add form. Adding a project creates a database row, not a
source-code project or folder. This workshop API has no endpoint authentication
or authorization and is **not secured for production**.

## Prerequisites

- .NET 10 SDK, PowerShell, and a modern browser.
- Complete the existing [API database configuration and schema readiness
  steps](https://github.com/JamieMBowers/ms-club-api/blob/master/ClubApi/README.md#configure-azure-sql-locally).
- Follow the [backend database readiness instructions](https://github.com/JamieMBowers/ms-club-api/blob/master/README.md#3-configure-your-azure-sql-database)
  for your Azure setup. The frontend itself does not apply migrations, manage
  firewall rules, or change cloud resources.
- Trust the local development certificate once, accepting the OS prompt:

  ```powershell
  dotnet dev-certs https --trust
  ```

## Run both apps

From the frontend repository root, use **two separate terminals** and leave
both running. These commands assume the backend is in the sibling
`..\ms_club_api` directory; see the [root setup guide](../README.md) to clone it.

Terminal 1 - existing API:

```powershell
dotnet run --project ..\ms_club_api\ClubApi\ClubApi.csproj --launch-profile https
```

Terminal 2 - frontend with Hot Reload:

```powershell
dotnet watch --project .\ClubWeb\ClubWeb.csproj run --launch-profile https
```

Before using the frontend, check <https://localhost:7240/api/projects> returns
`[]` or an array of records. A successful response proves a database read;
loading the Scalar documentation page alone does not. A new database has no
records until you add one.

Open **<https://localhost:7260/projects>**. The API remains at port **7240**,
with Scalar at <https://localhost:7240/scalar/v1>. The HTTP alternatives are
5090 (API) and 5260 (frontend); HTTPS is recommended. Both HTTPS launch
profiles select `Development`.

The root address <https://localhost:7260/> also opens Projects; there is no
separate Home page.

Watch mode automatically applies supported Razor, CSS, and C# edits and prompts
when a restart is needed. If another copy of ClubWeb is already using port 7260,
stop that copy before running this repository. For a one-time run without
watching, use `dotnet run --project .\ClubWeb\ClubWeb.csproj --launch-profile https`.

### API connection

The **frontend's** [appsettings.Development.json](appsettings.Development.json)
contains `ClubApi:BaseUrl`:

```json
{
  "ClubApi": {
    "BaseUrl": "https://localhost:7240/"
  }
}
```

Use the API address, not the frontend or Scalar URL, and keep the trailing slash.
[Program.cs](Program.cs) rejects missing/invalid settings, non-HTTP(S) URLs,
credentials, query strings, and fragments. The typed client appends paths such
as `api/projects`, has a 30-second timeout, and never automatically retries writes.

Outside Development, configure the frontend explicitly, for example:
`$env:ClubApi__BaseUrl = "https://your-api-host/"`.
Do not put database connection strings or access tokens in the frontend.

## Try the app

1. Open `/projects`. Existing records load automatically; **Refresh projects**
   repeats the list request.
2. In **Add a project**, enter a unique disposable name, such as
   `Workshop check <your own timestamp>`, then click **Add project**. Note its
   returned ID in the success message and list. Never assume ID 1.
3. Use **Search projects** to find that record. Click **View** to read its
   current details from the API.
4. Click **Rename**, edit the separate form, and choose **Save changes**.
   **Cancel rename** discards the local edit without making a request.
5. Refresh the browser. Projects load again automatically. Verify the same ID
   has the new name, demonstrating persistence.
6. Click **Delete** on your disposable record. Check its name and ID, then
   choose **Keep project**. No DELETE is sent; verify it still appears after
   refreshing.
7. Click **Delete** again, then **Delete permanently**. Verify it disappears.
8. In Scalar, send `GET /api/projects/{id}` using your recorded ID and verify
   **404**. Do not edit or delete pre-existing records.

Expand **Behind the scenes: API activity** to connect each action to its HTTP
request. A write and its subsequent list refresh are separate entries, so the
POST's 201/Location or PUT/DELETE's empty 204 is not lost behind the later GET.
Searching, cancelling, and opening delete confirmation are local UI actions.

Names must be nonblank and at most 100 characters. The API trims accepted names.
Failed creates keep the entered name; successful creates clear the form and
search so the new record is visible. Server `Name`/`name` errors map to the
appropriate field. Error details use safe local messages, never arbitrary HTML,
stack traces, or raw server exception text.

If a write succeeds but the following GET fails, the page says the change was
saved but the list could not refresh. **Refresh only; do not repeat the write.**
After an ambiguous write failure, further writes pause until a successful list
refresh. Check the records before retrying. A missing record during view,
rename, or delete produces a readable 404 message.

## HTTP contract

| App action | Method and route | Success |
|---|---|---|
| Initial load / Refresh projects | `GET /api/projects` | 200 with an array, possibly `[]` |
| View | `GET /api/projects/{id}` | 200 with a project |
| Add project | `POST /api/projects` | 201 with project JSON and Location |
| Save changes | `PUT /api/projects/{id}` | 204 with no body |
| Delete permanently | `DELETE /api/projects/{id}` | 204 with no body |

POST and PUT send only `{"name":"Campus Finder"}`. Responses contain integer
`id` and string `name`. Invalid writes return 400 validation details; a missing
single record returns 404.

**201** includes the generated ID in JSON. **204** means there is no response
body to deserialize.

## How it connects

`Browser -> Blazor server -> ProjectsApiClient -> ClubApi -> database`

The browser sends UI events over Blazor's circuit (normally a WebSocket).
The typed client calls the API from the **server**, so browser DevTools do not
show direct fetches to port 7240. Use API activity, terminal logs, breakpoints,
or Scalar to observe those requests.

Browser CORS does not apply to these server-to-server calls. The backend's
explicit-origin policy is unchanged. Do not use `AllowAnyOrigin` or bypass
certificate validation. A future browser-side client would need its specific
origin configured via `FRONTEND_ORIGIN` or `Cors:AllowedOrigins`.

## Teaching order

1. [Program.cs](Program.cs): DI, API base URL, and Interactive Server setup.
2. [Contracts](Contracts/ProjectResponse.cs): response and Name-only
   [create](Contracts/CreateProjectRequest.cs)/[update](Contracts/UpdateProjectRequest.cs)
   DTOs; [ProjectNameModel](Models/ProjectNameModel.cs) provides form validation.
3. [ProjectsApiClient](Services/ProjectsApiClient.cs): HTTP/JSON, cancellation,
   [response metadata](Services/ApiResponse.cs), and safe [errors](Services/ApiException.cs).
4. [Projects page](Components/Pages/Projects.razor): forms, binding, search,
   list rendering, and confirmation.
5. [ProjectCard](Components/ProjectCard.razor): parameters flow down;
   typed event callbacks flow up to the parent.
6. [Page handlers](Components/Pages/Projects.razor.cs): async actions,
   independent edit models, duplicate-submit guards, and separate write/refresh
   outcomes. [ApiResponsePanel](Components/ApiResponsePanel.razor) explains
   completed requests without owning HTTP calls.

[App.razor](Components/App.razor) makes Routes and HeadOutlet interactive server
components. Initial loading occurs in `OnAfterRenderAsync(firstRender)`, which
does not run during prerendering, preventing duplicate initial GETs.
Navigation cancels page-owned requests without showing cancellation errors.

## Automated checks

```powershell
dotnet build .\ClubWeb\ClubWeb.csproj
dotnet test .\ClubWeb.Tests\ClubWeb.Tests.csproj
```

Frontend tests use a fake in-memory `HttpMessageHandler`, not Azure SQL.
They cover the client contract, actual status/Location metadata, safe failures,
ambiguous write outcomes, cancellation, disposal, and exact form-validation
boundaries. Backend tests remain in the separate backend repository.
No component-testing framework was added; the browser flow above verifies UI
state transitions.

## Troubleshooting

| Symptom | Check |
|---|---|
| API `/` returns 404 | Expected: use `/scalar/v1` or `/api/projects`. The UI is on 7260. |
| API unavailable / wrong port | Start both HTTPS profiles. Check listening addresses and frontend `ClubApi:BaseUrl`. |
| Missing BaseUrl at startup | Use the Development profile or supply `ClubApi__BaseUrl`. Development JSON is not loaded in Production. |
| Certificate failure | Run `dotnet dev-certs https --trust`, accept the OS prompt, and restart both apps. |
| SQL error / timeout | Follow the existing backend readiness guide. Check Entra login, permissions, schema, firewall, and database wake-up. |
| Buttons do nothing | Check `_framework/blazor.web.js`, the circuit connection, and reconnect/error banner. Preserve InteractiveServer setup. |
| Saved change but stale list | Retry Refresh projects, not the successful mutation. |
| Old endpoint chooser still appears | Stop and restart ClubWeb, then refresh `/projects`. |
| No API fetches in browser DevTools | Expected: the typed client runs on the Blazor server. Expand API activity instead. |
