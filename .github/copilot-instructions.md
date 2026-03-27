# Copilot Instructions for AdvisorLeads

## Project Overview
AdvisorLeads is a .NET 8 WinForms desktop app (Windows-only) for recruiting financial advisors. It pulls data from FINRA BrokerCheck and SEC IAPD into a local SQLite database, and can export contacts to Wealthbox CRM.

## Build Commands
```powershell
# Restore and build
dotnet restore AdvisorLeads.slnx
dotnet build AdvisorLeads.slnx --configuration Release

# Publish self-contained Windows executable
dotnet publish src/AdvisorLeads/AdvisorLeads.csproj `
  --configuration Release --runtime win-x64 --self-contained true `
  --output publish/win-x64 -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true
```

There are no automated tests in this repository.

## Architecture

### Layers
- **Forms/** — WinForms UI (`MainForm`, dialogs)
- **Controls/** — Reusable UI components (`FilterPanel`, `AdvisorDetailCard`)
- **Services/** — Business logic and external API clients
- **Data/** — SQLite schema (`DatabaseContext`) and repository (`AdvisorRepository`)
- **Models/** — Plain C# model classes (no ORM)

### Data Flow
1. **First run**: `BackgroundDataService.PopulateInitialDataAsync()` downloads SEC gzipped XML files and hits the FINRA bulk API to seed the database.
2. **User search**: `FilterPanel` emits filter changes → `AdvisorRepository.GetAdvisors(SearchFilter)` queries SQLite → results shown in `ListView` → selection shows `AdvisorDetailCard`.
3. **Fetch new data**: `FetchDataDialog` → `DataSyncService.FetchAndSyncAsync()` → calls `FinraService` + `SecCompilationService` → merges by CRD number (fallback: name+firm+state) → `AdvisorRepository.UpsertAdvisor()`.
4. **Background refresh**: Runs every 60 minutes; fires `DataUpdated` event to reload the UI.
5. **CRM export**: `WealthboxService.ImportAdvisorAsync()` → POST/PUT to Wealthbox API → stores returned contact ID via `SetAdvisorImported()`.

### Key Services
| Service | Responsibility |
|---|---|
| `FinraService` | FINRA BrokerCheck REST API (JSON) |
| `SecCompilationService` | Downloads + parses SEC IAPD gzipped XML (~100 MB files) |
| `SecIapdService` | Stub — SEC has no public search API; returns empty results |
| `DataSyncService` | Orchestrates fetch, dedup, and upsert for FINRA + SEC |
| `BackgroundDataService` | Initial population and periodic bulk refresh |
| `WealthboxService` | Wealthbox CRM REST API integration |
| `AdvisorRepository` | All SQLite CRUD (raw SQL, no ORM) |
| `DatabaseContext` | Schema creation and connection management |

Services are instantiated directly in `MainForm` — there is no DI container.

## Database
- SQLite at `%APPDATA%\AdvisorLeads\advisorleads.db`; WAL mode and foreign keys enabled.
- Schema is created as raw SQL in `DatabaseContext.InitializeDatabase()` — no migrations framework.
- **Core tables**: `Advisors`, `Firms`, `EmploymentHistory`, `Disclosures`, `Qualifications`
- Upsert key: `CrdNumber` (UNIQUE). `UpsertAdvisor()` uses `INSERT OR REPLACE` logic.
- Indices on `LastName`, `State`, `FirmCrd`, `CrdNumber` on the `Advisors` table.

## Record Types

`Advisor.RecordType` (string) distinguishes individual registration category:
- `"Investment Advisor Representative"` — SEC-sourced IARs or FINRA records with active IA scope
- `"Registered Representative"` — FINRA-sourced RRs with active BC scope only

`Firm.RecordType`:
- `"Investment Advisor"` — all firms from the SEC RIA compilation file
- `"Broker-Dealer"` — reserved for FINRA-sourced firms (not yet fetched)

Set in `SecCompilationService` (default IAR, refined by `IndlCntyRgstrtns/RgstrtnCtgry`) and `FinraService` (derived from `ind_ia_scope` / `ind_bc_scope`). Filtered via `SearchFilter.RecordType` → exact match in `AdvisorRepository.GetAdvisors`.

## Conventions

### Naming
- Private fields: `_camelCase` (e.g., `_selectedAdvisor`, `_listView`)
- Constants: `PascalCase` property names on a static class (e.g., `MainSplitDefaultDistance = 220`)
- All public methods and properties: `PascalCase`

### Async Pattern
All network calls use `async/await` with `CancellationToken`. Progress is reported via `IProgress<string>`. Long-running UI operations disable controls and show status in the `StatusStrip`.

### Settings
Simple key=value file at `%APPDATA%\AdvisorLeads\settings.txt`. Read/write via `MainForm.LoadSetting(key)` / `SaveSetting(key, value)`. Currently stores only the Wealthbox API token.

### External API Notes
- **FINRA**: Mimics a browser (Chrome user-agent, `Origin`/`Referer` headers pointing to brokercheck.finra.org). Add **300 ms** delay between bulk queries to avoid rate limiting.
- **SEC**: Files are cached in `%APPDATA%\AdvisorLeads\SecCache\`. `HttpClient` timeout is 30 minutes for large XML downloads.
- **Wealthbox**: Bearer token auth. Add **200 ms** delay between batch import requests.

### SEC Cache
Downloaded SEC XML files are cached locally and reused across refreshes. Invalidation is time-based inside `SecCompilationService`.

## File Locations at Runtime
| Artifact | Path |
|---|---|
| Database | `%APPDATA%\AdvisorLeads\advisorleads.db` |
| Settings | `%APPDATA%\AdvisorLeads\settings.txt` |
| SEC XML cache | `%APPDATA%\AdvisorLeads\SecCache\` |
| Startup log | `%LOCALAPPDATA%\AdvisorLeads\startup.log` |

## Release Process
Push a `v*` tag to trigger the GitHub Actions release workflow (`.github/workflows/release.yml`), which builds a self-contained `win-x64` single-file executable and publishes it as a GitHub Release. Can also be triggered manually via workflow dispatch with a custom tag.
