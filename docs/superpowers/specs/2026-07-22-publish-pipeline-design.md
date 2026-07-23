# Publish Pipeline Design

Date: 2026-07-22
Branch: `dev/publish-pipeline`

## Problem

`ci.yml` pushes both packages to NuGet.org on every push to `main`. There is no
human gate, no git tag, no GitHub Release, and no release notes. Releasing should
be a deliberate act, separate from continuous integration.

## Goal

Adopt the two-stage model used by `brendankowitz/ignixa-fhir`:

1. **CI** builds, tests, and packs on every push to `main`, publishing nothing.
   The `.nupkg` files plus the computed version are retained as workflow artifacts.
2. **Publish Release** is run manually. It promotes the latest green CI build:
   pushes to NuGet.org, tags the commit, and creates a GitHub Release with
   AI-generated notes.

## Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Release notes | Claude via `anthropics/claude-code-action@v1` | Same action and `ANTHROPIC_API_KEY` secret already used by `claude.yml`; no new secret needed |
| CI output between releases | Workflow artifacts only | Nothing reaches any feed without a human |
| Tag scheme | `release/X.Y.Z` | Matches ignixa; release tags are visually distinct from other tags |
| Release source | Latest successful `ci.yml` run on `main` | One-click; no run-id bookkeeping |
| Manual controls | `skip_nuget`, `skip_tag` | Enables a dry run that previews the notes without shipping |
| Version continuity | Push `release/3.0.9` on `e2614ba` | Latest published is 3.0.9; the `tag-prefix` change hides the old bare tags, and this tag makes GitVersion compute 3.0.10 next without a `next-version` pin |

Explicitly out of scope: prerelease flag, `.nupkg` files as GitHub Release
assets, automated `CHANGELOG.md` updates, Docker, docs deployment.

## Changes

### `GitVersion.yml`

```yaml
workflow: TrunkBased/preview1
assembly-versioning-scheme: MajorMinorPatch
tag-prefix: 'release/'
```

`tag-prefix: 'release/'` makes GitVersion read only `release/*` tags, which
deliberately orphans the existing bare `2.0.0` / `2.0.2` tags from version
calculation. Continuity comes from a real tag rather than a config pin:
`release/3.0.9` is pushed on `e2614ba`, the commit whose CI run published 3.0.9
to NuGet.org. TrunkBased then increments from it, giving 3.0.10 next.

No `next-version` pin: a hardcoded floor drifts out of date silently and has to
be remembered on every release. (It also fails to parse under
`TrunkBased/preview1` — GitVersion rejects `next-version: 3.0.10` with
"Failed to parse 3.0.10 into a Semantic Version".)

### `.github/workflows/ci.yml`

Unchanged trigger (`push` to `main`, plus `workflow_dispatch`). Keeps calling the
reusable `build-and-test.yml`. The `publish` job is replaced by a `pack` job that
runs only on `main`:

1. Checkout with `fetch-depth: 0`, install and execute GitVersion to obtain
   `semVer` / `nuGetVersion`.
2. `dotnet pack` `Medino.csproj` and `Medino.Extensions.DependencyInjection.csproj`
   in Release into `./packages`.
3. Write `./packages/version.txt` (the version) and `./packages/commit-sha.txt`
   (`github.sha`).
4. Upload `./packages/*.nupkg`, `version.txt`, `commit-sha.txt` as artifact
   `nuget-packages`, 30-day retention.
5. Emit a step summary listing the package filenames and pointing at the
   Publish Release workflow.

The `dotnet nuget push` step is deleted.

### `.github/workflows/publish-release.yml` (new)

```yaml
on:
  workflow_dispatch:
    inputs:
      skip_nuget: { type: boolean, default: false }
      skip_tag:   { type: boolean, default: false }

permissions:
  contents: write   # push tag, create release
  id-token: write

concurrency:
  group: publish-release
  cancel-in-progress: false
```

Jobs:

**`prepare-release`** — checkout with full history; resolve the most recent
successful `ci.yml` run on `main` with `gh run list` and download its
`nuget-packages` artifact with `gh run download` (no third-party action, and
immune to artifact API version churn); read `version.txt` and `commit-sha.txt`
and fail with a clear message if either is empty or no `.nupkg` is present;
re-upload the `.nupkg` files as `release-packages` (1-day retention) for
downstream jobs. Outputs: `release_version`, `commit_sha`, `ci_run_id`.

**`publish-nuget`** — `needs: prepare-release`, `if: !inputs.skip_nuget`.
Downloads `release-packages`, sets up .NET, runs
`dotnet nuget push ./packages/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }}
--source https://api.nuget.org/v3/index.json --skip-duplicate`.

**`create-git-tag`** — `needs: [prepare-release, publish-nuget]`,
`if: !inputs.skip_tag && (publish-nuget succeeded || skipped)`. Creates and
pushes `release/${release_version}`; logs a warning and no-ops if the tag exists.

**`create-github-release`** — `needs: [prepare-release, create-git-tag]`,
`if: always() && prepare-release succeeded && (create-git-tag succeeded || skipped)`.

- Determines the commit range: previous `release/*` tag (version-sorted) to the
  new tag, or to `HEAD` in dry-run mode. Falls back to full history when no
  previous release tag exists.
- Collects `git log`, `gh pr view` details for referenced PR numbers, and issues
  closed since the previous release.
- Writes that to `release-context.md`, then runs `anthropics/claude-code-action@v1`
  (auth: `ANTHROPIC_API_KEY`, `--allowed-tools "Read,Write"`, `continue-on-error`)
  with a prompt asking for categorised markdown — ⚠️ Breaking Changes,
  🚀 Features, 🐛 Bug Fixes, 🔧 Maintenance, 📦 Dependencies — plus a Contributors
  section, PR/issue references as markdown links, and no invented content. The
  action writes `release_notes.md`.
- If generation fails or produces an empty file, falls back to a body containing
  the version heading and the raw commit list.
- Dry run (`create-git-tag` skipped): prints the notes to the job log and to the
  step summary; creates nothing.
- Otherwise `softprops/action-gh-release` with `tag_name: release/${version}`,
  `name: Release ${version}`, `body_path: release_notes.md`, no file assets.

**`release-summary`** — `if: always()`. Step-summary table of every stage's
result with ✅ / ❌ / ⏭️, the released version, and the tag name.

## Failure Handling

- Missing or empty `version.txt` / `commit-sha.txt` fails `prepare-release` with
  an explicit message rather than releasing an unknown version.
- `--skip-duplicate` makes a re-run after a partial NuGet push idempotent.
- An existing tag is a warning, not a failure, so a re-run after a mid-pipeline
  failure completes instead of aborting.
- Every job is independently re-runnable from the Actions UI.
- Release-note generation never blocks the release: it degrades to a commit list.

## Verification

Done:

- `actionlint` over both workflows — clean.
- YAML parse check of `ci.yml`, `publish-release.yml`, `GitVersion.yml`.
- `dotnet pack` locally for both packable projects — both packages produced.
- `dotnet-gitversion` on this branch — `MajorMinorPatch: 3.0.10`, confirming the
  `release/3.0.9` tag plus `tag-prefix` yields the intended next version.

Not provable locally:
- End-to-end behaviour (artifact hand-off, NuGet push, release creation) can only
  be proven by merging to `main` and running the workflows; the first real run is
  expected to use `skip_nuget: true, skip_tag: true` as a dry run.
