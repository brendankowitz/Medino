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
- For a minor or major bump, put `+semver: minor` or `+semver: major` in a commit
  message (or the squash-merge message of the PR that warrants it)

Release tags are created by the Publish Release workflow — don't tag by hand for a
routine release. The one hand-made tag is `release/3.0.9` on `e2614ba`, seeded when
the `release/` prefix was adopted so the 3.x stream continues from the last commit
published to NuGet.org rather than restarting. If that tag is ever deleted,
GitVersion loses its floor and starts numbering from scratch — recreate it on the
same commit rather than moving it.

## CI/CD

GitHub Actions workflows:

- **PR Validation** (`.github/workflows/pr.yml`) - Runs on pull requests
- **CI Build** (`.github/workflows/ci.yml`) - Runs on push to main/master
  - Builds and tests all target frameworks
  - On `main` only, packs both NuGet packages and uploads them as the
    `nuget-packages` artifact (30-day retention), together with `version.txt` and
    `commit-sha.txt`. Pushes to `master` build and test but produce no artifact.
  - Publishes nothing
- **Publish Release** (`.github/workflows/publish-release.yml`) - Manual only

## Releasing

Releasing is a deliberate, manual promotion of a build CI already produced.

1. Confirm the latest CI run on `main` is green, and that no CI run is still in
   progress. The workflow ships the most recent *completed successful* CI run —
   there is no way to pin an older one, so if someone merges and their CI goes
   green while you're releasing, you'll ship their commit too.
2. Actions → **🚀 Publish Release** → *Run workflow*.
   - Tick **both** **skip_nuget** and **skip_tag** for a dry run: it generates the
     release notes and prints them to the job summary without shipping anything.
   - Ticking only `skip_tag` still pushes to NuGet.org, which is irreversible.
   - Ticking only `skip_nuget` still creates the tag and the GitHub Release. That is
     the recovery path for "the packages are already on NuGet, finish the release" —
     used on anything else it burns a version on packages nobody can install.
3. Run it again with both unticked to release.

The workflow takes the packages from the latest successful CI run on `main`,
pushes them to NuGet.org, tags the built commit `release/<version>`, and creates
a GitHub Release whose notes Claude drafts from the commits, PRs, and issues
closed since the previous release. Note that CI cancels in-progress runs when a
newer commit lands on `main`, so a superseded commit never produces a package
artifact and can't be released on its own.

Jobs are independently re-runnable for 7 days after the run (how long the
intermediate `release-packages` artifact is kept): `--skip-duplicate` on the NuGet
push and the existing-tag check make a re-run after a partial failure safe. The tag
check fails loudly if the tag already exists on a *different* commit. After 7 days,
re-run Publish Release from scratch instead — which works for as long as the CI
`nuget-packages` artifact survives (30 days). Past that, the commit can only be
released by producing a fresh CI run on `main`.

### When something fails part-way

The job summary names the stage that stopped, and every stage is individually
re-runnable from the Actions UI:

| What happened | What to do |
|---|---|
| NuGet push failed | Re-run **📦 Publish to NuGet.org**; nothing was tagged or released yet |
| NuGet pushed, tagging failed | Re-run **🏷️ Create Git Tag** and the jobs after it |
| Tagged, GitHub Release failed | Re-run **🎁 Create GitHub Release**; the run is marked failed until it completes |
| Notes came out as a bare commit list | `ANTHROPIC_API_KEY` or the Claude action failed — the release still shipped; edit the release body by hand |
| Summary says "incomplete context" | A `gh` call for a PR or issue failed; the notes are missing some entries but nothing is broken |
| Run was cancelled mid-release | Check NuGet.org and the tag list before re-running — packages may already be public |
| 7-day artifact expired | Re-run the whole Publish Release workflow |
| 30-day CI artifact expired | Push an empty commit to `main` (or re-run CI) to produce a new build |

Required secrets: `NUGET_API_KEY` (a publish fails without it). `ANTHROPIC_API_KEY`
is optional — without it the release notes fall back to the raw commit list and the
release still completes.

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
