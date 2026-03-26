# AdvisorLeads
Lead tool for recruiting advisors

## Download

Download the latest release from the [Releases page](https://github.com/dannymayer/AdvisorLeads/releases).

The application is distributed as a self-contained Windows executable that doesn't require .NET to be installed.

### System Requirements
- Windows 10 or later (x64)
- No additional dependencies required

### Installation
1. Download the latest `AdvisorLeads-win-x64.zip` from the Releases page
2. Extract the zip file to a location of your choice
3. Run `AdvisorLeads.exe`

### Data Storage
The application stores its database and settings in: `%APPDATA%/AdvisorLeads/`

## Development

### Building from Source
```bash
dotnet restore AdvisorLeads.slnx
dotnet build AdvisorLeads.slnx --configuration Release
```

### Creating a Release
Releases are automatically created when a tag is pushed:

```bash
git tag -a v1.0.0 -m "Release version 1.0.0"
git push origin v1.0.0
```

The GitHub Actions workflow will automatically build the application and create a release with the executable.

You can also manually trigger a release from the Actions tab using the "Build and Release" workflow.
