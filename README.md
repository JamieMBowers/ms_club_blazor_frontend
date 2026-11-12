# ms_club_blazor_frontend

A beginner-friendly .NET 10 Blazor Interactive Server frontend for
[ms-club-api](https://github.com/JamieMBowers/ms-club-api).

Manage club projects with a form and searchable list:

- Automatically load projects from the existing API.
- Add projects, view details, rename, and confirm deletion.
- Search by name or ID.
- Expand optional API activity to inspect the HTTP requests behind the UI.

This repository contains **only the frontend and its tests**. The API, Azure
SQL configuration, migrations, and firewall tooling stay in the separate
backend repository. The frontend calls the API over HTTP(S), never the database
directly. The workshop backend has no endpoint authentication/authorization;
this is not a production-secured application.

## What you are running

```text
Browser -> Blazor frontend -> HTTP API -> Azure SQL database
           localhost:7260    localhost:7240
           this repository   backend repository
```

Both apps run on your computer in separate terminals. Only the database is
hosted in Azure; you do not need to deploy the apps to complete this workshop.
A "project" is a database row with an ID and name. **Add a project** does not
create a VS Code project, source files, or another API.

## 1. Install the tools

- [Git](https://git-scm.com/downloads).
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), not just the runtime.
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli-windows)
  for the backend's database access.
- Windows PowerShell and a browser. Use Windows PowerShell for the backend's
  SQL readiness script.
- Your own Azure subscription and Azure SQL database, configured in step 3.
- Optional: VS Code with C# Dev Kit.

Open a new terminal after installing tools and run:

```powershell
git --version
dotnet --list-sdks
az --version
dotnet dev-certs https --trust
```

Check that a `10.0.x` SDK is listed and accept the certificate trust prompt.

## 2. Clone both repositories

Run once in PowerShell:

```powershell
New-Item -ItemType Directory -Force "$HOME\club-workshop" | Out-Null
Set-Location "$HOME\club-workshop"
git clone https://github.com/JamieMBowers/ms-club-api.git .\ms_club_api
git clone https://github.com/JamieMBowers/ms_club_blazor_frontend.git .\ms_club_blazor_frontend
```

You should now have two sibling folders:

```text
club-workshop
  ms_club_api                  backend, migrations, and SQL readiness script
  ms_club_blazor_frontend      frontend and frontend tests
```

Already cloned them using the backend guide? Skip this step. If you use another
location, adjust the paths below. The explicit backend folder name uses
underscores; its GitHub repository name uses hyphens.

## 3. Set up the backend database first

Follow [step 3 of the backend student guide](https://github.com/JamieMBowers/ms-club-api/blob/master/README.md#3-configure-your-azure-sql-database)
in the **backend repository**, then return here. That step explains how to:

1. Create/select your own Azure SQL database and grant your account SQL access.
2. Sign in with `az login` and select your subscription.
3. Run the supplied SQL firewall/readiness script and wait for a successful check.
4. Set the API connection string using **backend .NET user-secrets**.
5. Apply the existing EF Core migration to create the `Projects` table.

**Do not skip this step.** The frontend uses real API data, not sample/mock
records. The firewall script does not configure the API connection string or
create its tables, and starting the API does not apply migrations.

Do not put a SQL connection string in this frontend. Its only backend setting
is `ClubApi:BaseUrl`, already set for local Development (see [API address](#api-address)).

## 4. Run both apps

Open **two separate terminals** and leave both running.

**Terminal 1 - backend**, from the backend repository root:

```powershell
Set-Location "$HOME\club-workshop\ms_club_api"
.\scripts\Update-SqlFirewall.ps1
dotnet run --project .\ClubApi\ClubApi.csproj --launch-profile https
```

Skip starting another backend instance if it is already running. If your Azure
login has expired, sign in again and select the subscription used in step 3.
The readiness script is useful after changing networks or an idle period.

Before starting the frontend, open <https://localhost:7240/api/projects>.
Expect `[]` for a new database or an array of project records. Fix any API/SQL
error first; simply opening Scalar does not verify database connectivity.

**Terminal 2 - frontend with Hot Reload**, from the frontend repository root:

```powershell
Set-Location "$HOME\club-workshop\ms_club_blazor_frontend"
dotnet watch --project .\ClubWeb\ClubWeb.csproj run --launch-profile https
```

Open **<https://localhost:7260/>**. You should see an **Add a project** form and
a projects list. The list is empty until you add records to a new database.
<https://localhost:7260/projects> opens the same page; there is no separate Home tab.

| Browser address | Purpose |
|---|---|
| <https://localhost:7260/> | The usable frontend: forms, search, and list |
| <https://localhost:7240/scalar/v1> | Scalar: explore and try the API endpoints |
| <https://localhost:7240/api/projects> | Raw data from the same API/database |

The API base URL `https://localhost:7240/` returns **404** because it has no
homepage. That is expected: the website is on **7260**.

### Hot Reload and stopping the apps

Most Razor, CSS, and supported C# edits update with Hot Reload. Changes that
cannot be hot-reloaded require a restart; watch mode will prompt you. From the
frontend repository root, run without watching using:

```powershell
dotnet run --project .\ClubWeb\ClubWeb.csproj --launch-profile https
```

If an older copy of the frontend is already using port 7260, stop that copy
before starting this repository's frontend.

Stop each app with **Ctrl+C** in its own terminal. For later sessions, repeat
step 4; there is no need to clone again or create another migration.

### Run in two VS Code windows

Open the repository folders separately. From the folder containing both clones:

```powershell
code --new-window .\ms_club_api
code --new-window .\ms_club_blazor_frontend
```

If the backend is already open, only run the second command. Install the
**C# Dev Kit** (or **C#**) extension. Each window selects its own solution
using `.vscode\settings.json`; no backend project reference is needed.

Complete the first-time database setup in step 3 before starting.

1. In the backend window's terminal, run
   `.\scripts\Update-SqlFirewall.ps1` from the backend repository root.
   Wait for the **database-ready message**. If your Azure login has expired,
   run `az login` first. The script is in the backend, not this repository.
2. In the backend window, select **ClubApi (https)** in **Run and Debug**
   and press **F5**. Wait for the listening message.
3. In the frontend window, select **ClubWeb (https)** and press **F5**.
4. Open **<https://localhost:7260/>** and keep both apps running.

If Windows blocks a file from running, follow
[If Windows blocks execution](#if-windows-blocks-execution) below.

The frontend configuration builds only `ClubWeb` and uses the Development
API address. **Ctrl+Shift+B** builds the frontend, and **Terminal > Run Task >
test ClubWeb** runs its tests. **Shift+F5** stops debugging in that window
without stopping the other app. Do not also start `dotnet watch` or `dotnet run`
on the same ports while debugging.

You can develop and build each repository independently, but frontend data
operations require the API to be running.

## 5. Try it and see how it works

1. Expand **Behind the scenes: API activity**. Initial loading sends
   `GET /api/projects`, returning **200**.
2. Add a unique disposable name, such as `My workshop demo 14-30`, and note its
   generated ID. The frontend sends `POST /api/projects`, returning **201**,
   then refreshes the list. You do not choose the ID yourself.
3. Open or refresh <https://localhost:7240/api/projects> in another browser tab.
   Find the same ID/name: the frontend and API are using the same stored data.
4. Search for your record, select **View**, then **Rename** and **Save changes**.
   View sends GET; saving the rename sends PUT and returns **204**, with no
   response body. Searching the loaded list does not send an API request.
5. Refresh the frontend page. Your renamed record should still be there.
   Stop/restart the API and refresh again to verify it survives an API restart:
   the data lives in SQL, not just the browser or API memory.
6. Select **Delete** and choose **Keep project** first. No DELETE is sent.
   Then delete again and choose **Delete permanently** for your own test record.
   Expect **204** and confirm the record disappears from the UI and JSON tab.

Only modify/delete records you created. Names must be nonblank and at most
100 characters. The activity panel keeps the last six exchanges, including
the separate list refresh after a write, so inspect entries as you go.

See the [detailed app guide](ClubWeb/README.md#try-the-app) for a longer exercise
and the [HTTP contract](ClubWeb/README.md#http-contract) for each action's endpoint.

## API address

[ClubWeb/appsettings.Development.json](ClubWeb/appsettings.Development.json)
configures the API address:

```json
{
  "ClubApi": {
    "BaseUrl": "https://localhost:7240/"
  }
}
```

Use an absolute HTTP(S) URL with a trailing slash. This is the **backend**
address, not the frontend's address. Outside Development, supply the setting
explicitly, for example `$env:ClubApi__BaseUrl = "https://your-api-host/"`.

API requests originate on the Blazor server. They do not appear as direct
browser fetches to the backend and do not require browser CORS changes.
Do not add SQL connection strings or access tokens to this repository.

## Build and test

Run from the **frontend repository root**:

```powershell
dotnet build .\ms_club_blazor_frontend.sln
dotnet test .\ClubWeb.Tests\ClubWeb.Tests.csproj
```

Client tests use an in-memory fake HTTP handler and never connect to Azure SQL.
The solution references no backend project and builds without the backend
repository present.

## Troubleshooting

| Symptom | What to do |
|---|---|
| No projects on first run | An empty list is normal for a new database. Add a record; there is no sample data. An error banner is not the same as an empty list. |
| API unavailable or list fails to load | Keep both terminals running. Check `/api/projects` on port 7240 first, then `ClubApi:BaseUrl`. |
| API returns 500 / SQL timeout | Recheck the [backend setup](https://github.com/JamieMBowers/ms-club-api/blob/master/README.md#3-configure-your-azure-sql-database): login, SQL permissions, firewall/warm-up, connection string, and migration. |
| HTTPS certificate error | Run `dotnet dev-certs https --trust`, accept the prompt, and restart both apps/browser. Do not disable certificate validation. |
| Port already in use | Stop the older copy in its terminal; do not run two copies on the same port. |
| Reconnect/disconnected banner or inactive buttons | Keep the Blazor terminal running; restart the frontend if needed and reload the page. |
| Saved change but list refresh failed | Use **Refresh projects**, not another add/rename/delete. Check existing data before retrying an uncertain write. |
| No API requests in browser DevTools | Expected with Interactive Server. Expand **Behind the scenes: API activity** or use Scalar instead. |
| Missing API BaseUrl setting | Use the `https` launch profile (Development), or explicitly set `ClubApi__BaseUrl` for another environment. |

### If Windows blocks execution

**Error `0x800711C7` means Windows Application Control blocked a file from
running.** This is a Windows security-policy restriction, not a compiler error
or an Azure SQL firewall error.

During our local setup, both solutions **built successfully**, but Windows
blocked the **test assemblies**. No tests executed, so their results could not
be verified. This does **not** establish that the API or frontend is blocked
too; try the normal startup steps and check the actual error.

If you see this error:

1. Stop retrying the blocked command. Save the error code, blocked file name,
   and what you were trying to run. Do not share passwords or access tokens.
2. Ask your teacher or IT administrator for help. They can inspect
   **Event Viewer > Applications and Services Logs > Microsoft > Windows >
   CodeIntegrity > Operational** and determine which development files or
   tools need approval under the device's policy.
3. After approval, retry the same command or F5 launch. If tests were blocked,
   rerun them and confirm that tests actually execute.

**Do not disable Windows security or download unofficial "fix" tools.**
Changing Azure SQL firewall rules will not fix this Windows execution block.

## Project layout

- [ClubWeb](ClubWeb): Blazor application, typed API client, contracts, and forms.
- [ClubWeb.Tests](ClubWeb.Tests): client contract, error-handling, and validation tests.
- [Detailed run and teaching guide](ClubWeb/README.md): UI walkthrough, HTTP
  contract, file-by-file explanation, and troubleshooting.
