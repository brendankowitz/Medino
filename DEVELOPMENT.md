# Development Guide

## Setup

```bash
# Clone the repository
git clone https://github.com/brendankowitz/Medino.git
cd Medino

# Restore dotnet tools (includes reportgenerator for coverage)
dotnet tool restore

# Restore dependencies
dotnet restore src/Medino.sln
```

## Building

```bash
# Build all projects
dotnet build src/Medino.sln

# Build in Release mode
dotnet build src/Medino.sln --configuration Release

# Clean build
dotnet clean src/Medino.sln
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

The project uses **GitVersion** in mainline mode:
- Version is calculated from git tags and commits
- Tag format: `2.0.0`, `2.1.0`, etc.
- Each commit after a tag increments the patch version

Create a new version:
```bash
# Create and push a version tag
git tag 2.1.0
git push origin 2.1.0
```

## CI/CD

GitHub Actions workflows:

- **PR Validation** (`.github/workflows/pr.yml`) - Runs on pull requests
- **CI Build** (`.github/workflows/ci.yml`) - Runs on push to main/master
  - Builds and tests all target frameworks
  - Publishes NuGet packages to nuget.org (only on main branch)

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
