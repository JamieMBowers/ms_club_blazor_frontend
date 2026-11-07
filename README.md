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

## Prerequisites

- .NET 10 SDK.
- PowerShell and a browser.
- The [backend's database setup](https://github.com/JamieMBowers/ms-club-api/blob/master/ClubApi/README.md#configure-azure-sql-locally)
  and [readiness steps](https://github.com/JamieMBowers/ms-club-api/blob/master/README.md#1-refresh-the-azure-sql-firewall-rule-and-wake-the-database)
  completed in the backend repository.

Clone this frontend:

```powershell
git clone https://github.com/JamieMBowers/ms_club_blazor_frontend.git
Set-Location .\ms_club_blazor_frontend
```

The commands below assume the backend is in a sibling folder named
`ms_club_api`. If it is not already present, clone it once:

```powershell
git clone https://github.com/JamieMBowers/ms-club-api.git ..\ms_club_api
```

Trust the local HTTPS certificate once:

```powershell
dotnet dev-certs https --trust
```

## Run locally with Hot Reload

From the **frontend repository root**, use two terminals.

**Terminal 1 - backend** (skip if it is already running):

```powershell
dotnet run --project ..\ms_club_api\ClubApi\ClubApi.csproj --launch-profile https
```

**Terminal 2 - frontend with Hot Reload**:

```powershell
dotnet watch --project .\ClubWeb\ClubWeb.csproj run --launch-profile https
```

Open **<https://localhost:7260/>** or **<https://localhost:7260/projects>**.
The API listens on **7240**, with Scalar at <https://localhost:7240/scalar/v1>.

Most Razor, CSS, and supported C# edits update with Hot Reload. Changes that
cannot be hot-reloaded require a restart; watch mode will prompt you. To run
without watching, use:

```powershell
dotnet run --project .\ClubWeb\ClubWeb.csproj --launch-profile https
```

If an older copy of the frontend is already using port 7260, stop that copy
before starting this repository's frontend.

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

```powershell
dotnet build .\ms_club_blazor_frontend.sln
dotnet test .\ClubWeb.Tests\ClubWeb.Tests.csproj
```

Client tests use an in-memory fake HTTP handler and never connect to Azure SQL.
The solution references no backend project and builds without the backend
repository present.

## Project layout

- [ClubWeb](ClubWeb): Blazor application, typed API client, contracts, and forms.
- [ClubWeb.Tests](ClubWeb.Tests): client contract, error-handling, and validation tests.
- [Detailed run and teaching guide](ClubWeb/README.md): UI walkthrough, HTTP
  contract, file-by-file explanation, and troubleshooting.
