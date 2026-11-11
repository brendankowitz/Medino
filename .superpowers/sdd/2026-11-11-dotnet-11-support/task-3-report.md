# Task 3 Report: .NET support documentation

## Status

Completed the documentation updates for Task 3. No public APIs, runtime behavior, project files, workflows, or release-process instructions were changed.

## Changes

- Updated the root and package READMEs to state that the supported target frameworks are `net10.0` and `net11.0`, with .NET 10 as the minimum supported runtime.
- Added the .NET support transition to `MIGRATION.md` and `CHANGELOG.md`: the next major release removes `net8.0` and `net9.0` assets, and affected consumers must move to .NET 10 or .NET 11.
- Updated `DEVELOPMENT.md` and `CLAUDE.md` to reflect the stable .NET 10 and .NET 11 SDK setup, package/test target frameworks, and separate test runs per framework.
- Documented that parameterless `AddMedino()` calls `Assembly.GetCallingAssembly()` and may throw on .NET 11 when stack-trace support is disabled. The DI package README points affected applications to the existing explicit-assembly overload and clarifies that registration behavior is unchanged.
- Updated the DI package README's dependency requirement to match the package's `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.9 reference.

## Validation

- Ran the requested command:
  `rg -n "net8\\.0|net9\\.0|\\.NET 8|\\.NET 9" README.md DEVELOPMENT.md MIGRATION.md CLAUDE.md src`
  The only result was the `MIGRATION.md` statement explaining removal of the old assets.
- Also searched `CHANGELOG.md`. Remaining .NET 8/9 references are the historical version 2.0.0 support announcement and the new removal notice.
- `git diff --check` passed.
- Verified the documented explicit-assembly overload exists and the parameterless overload delegates through `Assembly.GetCallingAssembly()`.
- No documentation site or site-build configuration (`docs/site`, package manifest, MkDocs config, or Docusaurus config) exists in this repository, so a documentation-site build is not applicable.

## Self-review

The support statements match the package and test project target frameworks and the shared workflow's SDK/test matrix. Existing README usage examples and unrelated release guidance were preserved. Only Task 3 documentation files and this requested report were changed by this task.

## Round 1 fix

### Change

Qualified the `Microsoft.Extensions.DependencyInjection.Abstractions` requirement by target framework in `src/Medino.Extensions.DependencyInjection/README.md`: version 10.0.9 or later is required for the `net10.0` asset, while the .NET 11 shared framework supplies it for `net11.0`. The supported framework matrix and .NET 11 parameterless `AddMedino()` reflection caveat are unchanged.

### Validation

Commands run:

```text
rg -n "net8\.0|net9\.0|\.NET 8|\.NET 9" README.md DEVELOPMENT.md MIGRATION.md CLAUDE.md src
git diff --check
```

Output:

```text
MIGRATION.md:7:The next major release removes the `net8.0` and `net9.0` package assets. Applications using this release must target .NET 10 or .NET 11; .NET 10 is the minimum supported runtime. Retarget affected applications and update their SDK/runtime deployment as needed before upgrading.
```

`git diff --check` produced no output and exited successfully.
