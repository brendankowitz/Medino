# Development Guide

## Setup

```bash
# Clone the repository
git clone https://github.com/brendankowitz/Medino.git
cd Medino

# Restore dotnet tools (includes reportgenerator for coverage)
dotnet tool restore

# Restore dependencies
dotnet restore src/Medino.slnx
```

## Building

```bash
# Build all projects
dotnet build src/Medino.slnx

# Build in Release mode
dotnet build src/Medino.slnx --configuration Release

# Clean build
dotnet clean src/Medino.slnx
```

## Testing

```bash
# Run all tests
dotnet test src/Medino.Tests/Medino.Tests.csproj

# Run tests with detailed output
dotnet test src/Medino.Tests/Medino.Tests.csproj --verbosity normal

# Run specific test
dotnet test --filter "FullyQualifiedName~TestMethodName"
```

## Code Coverage

```bash
# Run tests with coverage collection
dotnet test src/Medino.Tests/Medino.Tests.csproj --collect:"XPlat Code Coverage" --results-directory ./TestResults

# Generate HTML coverage report
dotnet reportgenerator -reports:TestResults/**/coverage.cobertura.xml -targetdir:TestResults/CoverageReport -reporttypes:Html

# View coverage report (Windows)
start TestResults/CoverageReport/index.html

# View coverage report (macOS)
open TestResults/CoverageReport/index.html

# View coverage report (Linux)
xdg-open TestResults/CoverageReport/index.html
```

### Coverage Requirements

All Medino core library components must maintain **≥80% code coverage**:
- `Medino` core library: ≥80%
- `Medino.Extensions.DependencyInjection`: ≥80%

## VS Code

The project includes pre-configured tasks accessible via `Ctrl+Shift+P` → "Tasks: Run Task":

- **build** (Ctrl+Shift+B) - Build the solution
- **clean** - Clean build artifacts
- **test** - Run all tests
- **build and test** - Build and test in sequence
- **test with coverage** - Run tests with coverage collection
- **coverage report** - Generate and open HTML coverage report
- **watch tests** - Continuously run tests on file changes

## Packaging

```bash
# Create NuGet packages
dotnet pack src/Medino/Medino.csproj --configuration Release --output artifacts
dotnet pack src/Medino.Extensions.DependencyInjection/Medino.Extensions.DependencyInjection.csproj --configuration Release --output artifacts
```

Packages are created with:
- GitVersion for automatic semantic versioning
- SourceLink for debugging support
- Reproducible builds enabled

## Versioning

The project uses **GitVersion** in trunk-based mode:
- Version is calculated from `release/*` tags and the commits after them
- Tag format: `release/3.0.10`, `release/3.1.0`, etc.
- Each commit after a tag increments the patch version

Release tags are created by the Publish Release workflow — don't tag by hand.

## CI/CD

GitHub Actions workflows:

- **PR Validation** (`.github/workflows/pr.yml`) - Runs on pull requests
- **CI Build** (`.github/workflows/ci.yml`) - Runs on push to main/master
  - Builds and tests all target frameworks
  - Packs both NuGet packages and uploads them as the `nuget-packages` artifact
    (30-day retention), together with `version.txt` and `commit-sha.txt`
  - Publishes nothing
- **Publish Release** (`.github/workflows/publish-release.yml`) - Manual only

## Releasing

Releasing is a deliberate, manual promotion of a build CI already produced.

1. Confirm the CI run for the commit you want to ship is green on `main`.
2. Actions → **🚀 Publish Release** → *Run workflow*.
   - Tick both **skip_nuget** and **skip_tag** for a dry run: it generates the
     release notes and prints them to the job summary without shipping anything.
3. Run it again with both unticked to release.

The workflow takes the packages from the latest successful CI run on `main`,
pushes them to NuGet.org, tags the built commit `release/<version>`, and creates
a GitHub Release whose notes Claude drafts from the commits, PRs, and issues
closed since the previous release.

Every job is independently re-runnable — `--skip-duplicate` on the NuGet push and
the existing-tag check make a re-run after a partial failure safe.

Required secrets: `NUGET_API_KEY`, `ANTHROPIC_API_KEY`.

## Tools

Required tools are managed via `.config/dotnet-tools.json`:
- **reportgenerator** - Code coverage report generation

Install all tools:
```bash
dotnet tool restore
```

## Project Structure

```
src/
├── Medino/                                    # Core library
├── Medino.Extensions.DependencyInjection/    # Microsoft DI integration
└── Medino.Tests/                             # Test suite
```
