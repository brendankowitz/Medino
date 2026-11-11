# Task 2 implementation report: .NET 11 CI support

## Status

Implemented the CI SDK and test-artifact changes in the two scoped workflow files.

## Changes

- `.github/workflows/build-and-test.yml` and `.github/workflows/ci.yml` now install stable `10.0.x` and `11.0.x` SDKs, retaining `dotnet-quality: 'ga'`.
- The build-and-test workflow runs tests once for `net10.0` and once for `net11.0`. Each target writes a distinct TRX filename under its own results directory; the upload glob includes both.
- Coverage collection remains enabled in each test run, and the existing coverage upload path is unchanged.
- Workflow triggers are unchanged. `.github/workflows/publish-release.yml` remains untouched because package publication consumes already-packed artifacts and the requested package checks succeeded without changing it.
- No package/project files or dependency updates were included. Dependency PRs #50 and #51 remain separate.

## Validation

- Parsed both modified workflow files with PyYAML 6.0.3 and asserted the SDK versions, GA quality, per-target TRX naming, upload path, and coverage collection/upload configuration. `actionlint` was not installed in the environment.
- Ran both requested Release `dotnet pack` commands with the provided SDK 11.0.100. Both packages were created successfully.
- Inspected both `.nupkg` files: each contains `net10.0` and `net11.0` library assets. The DI package's `net10.0` dependency group includes `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.9; its `net11.0` group includes Medino and correctly omits the .NET 10-only abstractions dependency.
- Built the solution and ran the two target-specific test commands. Both TRX files were produced under distinct paths, with 66/66 tests passing for each TFM. Coverage reports were produced for both targets.
- `git diff --check` passed.

## Concerns

Pack/build emitted the pre-existing NU1902 advisory for `Microsoft.Build.Tasks.Git` 10.0.300. This task did not alter that dependency.

## Commit

See the completion response for the resulting commit SHA and subject.
