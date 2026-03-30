# AdvisorLeads

A portable .NET 8 WinForms desktop application for researching and recruiting financial advisors. It aggregates data from FINRA BrokerCheck, SEC IAPD, and SEC EDGAR into a local SQLite database and provides tools for filtering, scoring, and exporting advisor contacts.

## Documentation

- 📖 [Getting Started Guide](docs/getting-started.md) — Download, first launch, finding advisors, saving favorites

## Download

Download the latest release from the [Releases page](https://github.com/dannymayer/AdvisorLeads/releases).

The application is distributed as a self-contained Windows executable — no .NET runtime or external database required.

### System Requirements

- Windows 10 or later (x64)
- Internet connection (for data fetching from FINRA/SEC)
- No additional dependencies

### Installation

1. Download `AdvisorLeads-win-x64.zip` from the Releases page
2. Extract to any folder
3. Run `AdvisorLeads.exe`

On first launch the app downloads SEC compilation files and FINRA bulk data to seed the local database. This may take several minutes depending on your connection.

## Features

- **Advisor Search & Filtering** — Filter by name, state, firm, record type (IAR / RR), CRD number, with paginated card-based results
- **Firm Research** — Browse and filter investment advisor firms with AUM, employee count, and filing history
- **SEC EDGAR Intelligence** — AUM growth analytics, change detection across filings, Form ADV historical analysis, EDGAR full-text search, and M&A target scoring
- **Favorites & Lists** — Mark advisors as favorites, organize into custom lists, exclude uninteresting records
- **Wealthbox CRM Export** — Push advisor contacts to Wealthbox CRM via API
- **Background Data Refresh** — Automatic hourly refresh keeps data current without manual intervention
- **Portable** — All data stored locally in SQLite; no external database server needed

## Architecture

```
src/AdvisorLeads/
├── Controls/          # Reusable UI components
│   ├── AdvisorCard        Card rendering for each advisor in the center panel
│   ├── AdvisorDetailCard  Right-side detail view for a selected advisor
│   ├── FilterPanel        Left-side search/filter controls (advisors)
│   ├── FirmDetailPanel    Right-side detail view for a selected firm
│   └── FirmFilterPanel    Top filter bar for firm search
├── Data/              # Database layer (EF Core + SQLite)
│   ├── DatabaseContext     EF Core DbContext, schema creation, migration
│   ├── AdvisorRepository   CRUD for advisors, firms, related entities
│   └── ListRepository      CRUD for custom advisor lists
├── Forms/             # WinForms windows and dialogs
│   ├── MainForm            Primary application window
│   ├── FetchDataDialog     Manual data fetch with progress
│   ├── ListManagerForm     Create/manage advisor lists
│   └── ...                 Settings, exclusion, and list dialogs
├── Models/            # Plain C# model classes
│   ├── Advisor, Firm       Core domain models
│   ├── SearchFilter        Filter/pagination parameters
│   └── ...                 EDGAR models, filing models
├── Services/          # Business logic and API clients
│   ├── FinraService        FINRA BrokerCheck REST API
│   ├── SecCompilationService  SEC IAPD gzipped XML downloads
│   ├── DataSyncService     Orchestrates fetch, dedup, upsert
│   ├── BackgroundDataService  Initial population + periodic refresh
│   ├── AumAnalyticsService    AUM growth metrics from EDGAR
│   ├── ChangeDetectionService Firm data diff across filings
│   ├── EdgarSearchService     SEC EDGAR full-text search (EFTS)
│   ├── MaTargetScoringService M&A target composite scoring
│   ├── WealthboxService    Wealthbox CRM integration
│   └── ...                 Additional SEC/EDGAR services
└── Program.cs         # Application entry point
```

### Data Flow

1. **Seed** — `BackgroundDataService` downloads SEC gzipped XML and FINRA bulk data on first launch
2. **Search** — `FilterPanel` emits filter changes → `AdvisorRepository.GetAdvisors()` queries SQLite → paginated card results
3. **Fetch** — `FetchDataDialog` → `DataSyncService` → calls `FinraService` + `SecCompilationService` → merges by CRD → `UpsertAdvisor()`
4. **Refresh** — Background timer fires every 60 minutes, re-syncs from FINRA/SEC
5. **Export** — `WealthboxService.ImportAdvisorAsync()` → POST/PUT to Wealthbox API

### Database

- **Engine**: SQLite via EF Core 8 (`Microsoft.EntityFrameworkCore.Sqlite`)
- **Location**: `%APPDATA%\AdvisorLeads\advisorleads.db`
- **Mode**: WAL (Write-Ahead Logging) with foreign keys enabled
- **Schema migration**: `ApplySchemaUpgrades()` detects missing columns/tables and adds them automatically — no migration framework needed
- **Thread safety**: Repository methods use a fresh `DbContext` per call (`CreateContext()` factory pattern)

Core tables: `Advisors`, `Firms`, `EmploymentHistory`, `Disclosures`, `Qualifications`, `AdvisorLists`, `AdvisorListMembers`, `FirmAumHistory`, `FirmOwnership`, `FormAdvFilings`, `FirmFilings`, `FirmFilingEvents`, `EdgarSearchResults`

### External APIs

| API | Purpose | Notes |
|-----|---------|-------|
| FINRA BrokerCheck | Advisor/firm search and detail | Browser-mimicking headers; 300ms delay between bulk queries |
| SEC IAPD | Compilation XML files (~100 MB) | Cached in `%APPDATA%\AdvisorLeads\SecCache\`; 30-min HTTP timeout |
| SEC EDGAR | EFTS search, submissions data | `data.sec.gov` and `efts.sec.gov` endpoints |
| Wealthbox CRM | Contact export | Bearer token auth; 200ms delay between requests |

## Data Storage

All application data is stored locally — no external database server required.

| Artifact | Location |
|----------|----------|
| Database | `%APPDATA%\AdvisorLeads\advisorleads.db` |
| Settings | `%APPDATA%\AdvisorLeads\settings.txt` |
| SEC cache | `%APPDATA%\AdvisorLeads\SecCache\` |
| Startup log | `%LOCALAPPDATA%\AdvisorLeads\startup.log` |

## Development

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows)

### Building

```powershell
dotnet restore AdvisorLeads.slnx
dotnet build AdvisorLeads.slnx --configuration Release
```

### Running Tests

```powershell
dotnet test tests/AdvisorLeads.Tests --configuration Release
```

The test suite includes 44 tests covering:
- Database schema creation and migration
- Repository CRUD operations and filtering
- Model computed properties and defaults
- Formatting helpers

### Publishing

Build a self-contained single-file executable:

```powershell
dotnet publish src/AdvisorLeads/AdvisorLeads.csproj `
  --configuration Release --runtime win-x64 --self-contained true `
  --output publish/win-x64 -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true
```

### Creating a Release

Push a version tag to trigger the CI release workflow:

```bash
git tag -a v1.0.0 -m "Release version 1.0.0"
git push origin v1.0.0
```

The GitHub Actions workflow (`.github/workflows/release.yml`) builds the executable and publishes it as a GitHub Release. You can also trigger it manually from the Actions tab.

## Configuration

Settings are stored in `%APPDATA%\AdvisorLeads\settings.txt` as simple `key=value` pairs:

| Key | Description |
|-----|-------------|
| `WealthboxToken` | API token for Wealthbox CRM export |

## License

See [LICENSE](LICENSE) for details.
