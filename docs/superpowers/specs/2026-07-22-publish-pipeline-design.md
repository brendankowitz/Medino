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
be remembered on every release. It is also unavailable here — GitVersion parses
the configured `next-version` through `tag-prefix`, so with `tag-prefix:
'release/'` a bare `next-version: '3.0.10'` fails with "Failed to parse 3.0.10
into a Semantic Version". Verified by toggling `tag-prefix` with the pin held
constant: it parses without the prefix and fails with it.

### `.github/workflows/ci.yml`

Unchanged trigger (`push` to `master`/`main`, plus `workflow_dispatch`); a new
`concurrency` group cancels superseded runs on the same ref. Keeps calling the
reusable `build-and-test.yml`. The `publish` job is replaced by a `pack` job
(`permissions: contents: read`) gated to `main`:

1. Checkout with `fetch-depth: 0`, install and execute GitVersion to obtain
   `semVer`. (GitVersion 6 has no `NuGetVersion` output — `semVer` is what
   `GitVersion.MsBuild` stamps into the package.)
2. `dotnet pack` `Medino.csproj` and `Medino.Extensions.DependencyInjection.csproj`
   in Release into `./packages`.
3. Assert `./packages/<id>.<semVer>.nupkg` exists for both package ids. The CLI
   and the MSBuild task compute the version independently; this makes a
   divergence a hard failure instead of a release that ships one version and
   announces another. It also catches a build where only one package was packed.
4. Write `./packages/version.txt` (the version) and `./packages/commit-sha.txt`
   (`github.sha`).
5. Upload `./packages/*.nupkg`, `version.txt`, `commit-sha.txt` as artifact
   `nuget-packages`, 30-day retention, `if-no-files-found: error`.
6. Emit a step summary listing the package filenames and pointing at the
   Publish Release workflow.

The `dotnet nuget push` step is deleted.

Consequence of the concurrency group: a commit superseded by a newer push to
`main` never produces a package artifact, so it cannot be released on its own.

### `.github/workflows/publish-release.yml` (new)

```yaml
on:
  workflow_dispatch:
    inputs:
      skip_nuget: { type: boolean, default: false }
      skip_tag:   { type: boolean, default: false }

permissions:
  contents: read    # jobs that write opt in individually

concurrency:
  group: publish-release
  cancel-in-progress: false
```

Permissions are per job: `contents: read` + `actions: read` for the artifact
jobs, `contents: write` only for `create-git-tag` and `create-github-release`,
`contents/pull-requests/issues: read` for note generation, none for the summary.
No `id-token: write` — nothing here uses OIDC.

Jobs:

**`prepare-release`** — checkout with full history; resolve the most recent
successful `ci.yml` run on `main` with `gh run list` and download its
`nuget-packages` artifact with `gh run download` (no third-party action, and
immune to artifact API version churn); read `version.txt` and `commit-sha.txt`
and fail with a clear message if either is empty or no `.nupkg` is present;
re-verify both expected `.nupkg` filenames against the recorded version; re-upload
the `.nupkg` files as `release-packages` (7-day retention) for downstream jobs.
Outputs: `release_version`, `commit_sha`, `ci_run_id`.

**`publish-nuget`** — `needs: prepare-release`, `if: !inputs.skip_nuget`.
Downloads `release-packages`, sets up .NET, runs `dotnet nuget push
./packages/*.nupkg --source https://api.nuget.org/v3/index.json --skip-duplicate`
with the API key bound through `env:`. The push output is teed and scanned: if
anything was skipped as a duplicate, a warning says so, because `--skip-duplicate`
otherwise makes "this version is already published" indistinguishable from a
successful release.

**`create-git-tag`** — `needs: [prepare-release, publish-nuget]`,
`permissions: contents: write`, `if: !cancelled() && !inputs.skip_tag &&
prepare-release succeeded && (publish-nuget succeeded || skipped)`. The
`!cancelled()` prefix is required: without a status-check function GitHub applies
an implicit `success()` over `needs`, and a *skipped* `publish-nuget` would skip
this job before the `'skipped'` branch could be evaluated — silently breaking the
`skip_nuget: true, skip_tag: false` combination.

Tags `release/${release_version}` at the recorded `commit_sha`. If the tag already
exists it is a no-op when it points at that same commit, and a hard failure when
it points somewhere else (otherwise the release would be attached to a tag
describing different code than the packages being pushed).

**`generate-release-notes`** — `needs: [prepare-release, create-git-tag]`,
`permissions: contents/pull-requests/issues: read`,
`if: always() && prepare-release succeeded && (create-git-tag succeeded ||
(skipped && inputs.skip_tag))`. The dry-run branch keys off the *input*, not off
`create-git-tag` being skipped, because that job is also skipped when the NuGet
push fails — which is not a dry run.

A separate, read-only job by design: it feeds untrusted text to a model, so it
must not hold a token that can write to the repository.

- Determines the commit range: previous `release/*` tag (version-sorted) to the
  new tag, or to the artifact's `commit_sha` in dry-run mode — not `HEAD`, which
  may have moved past the commit being released. Falls back to full history when
  no previous release tag exists.
- Collects `git log`, `gh pr view` details for referenced PR numbers, and issues
  closed since the previous release. A `#123` that isn't a PR is skipped; any
  other `gh` failure (403, rate limit, network) fails the step rather than
  silently producing context-free notes.
- Writes that to `release-context.md` with control characters stripped, then runs
  `anthropics/claude-code-action@v1` (auth: `ANTHROPIC_API_KEY`,
  `--allowed-tools "Read,Write"`, `continue-on-error`) with a prompt asking for
  categorised markdown — ⚠️ Breaking Changes, 🚀 Features, 🐛 Bug Fixes,
  🔧 Maintenance, 📦 Dependencies — plus a Contributors section, PR/issue
  references as markdown links, and no invented content. The prompt states that
  the context is untrusted data and that instructions inside it must not be
  followed. The action writes `release_notes.md`.
- The verify step checks the action's `outcome` as well as the file (non-empty
  and containing at least one `##` section), falls back to the version heading
  plus the raw commit list otherwise, and exports `notes_ok` so the degradation
  is visible in the summary instead of only in an annotation.
- Uploads `release_notes.md` as the `release-notes` artifact and prints it to the
  step summary — which is the entire output of a dry run.

**`create-github-release`** — `needs: [prepare-release, create-git-tag,
generate-release-notes]`, `permissions: contents: write`, runs only when both the
tag and the notes succeeded. Downloads the notes artifact and calls
`softprops/action-gh-release` with `tag_name: release/${version}`,
`name: Release ${version}`, `body_path: release_notes.md`, no file assets.

**`release-summary`** — `if: always()`, `permissions: {}`. Step-summary table of
every stage (including prepare and note generation, flagging a commit-list
fallback) with ✅ / ❌ / ⏭️, plus the version and source CI run id. It reports what
happened rather than mirroring job results: any failure exits non-zero, a run
where nothing was published or tagged is labelled a dry run, and the NuGet URL
and tag name are printed only when those stages actually succeeded.

## Failure Handling

- Missing or empty `version.txt` / `commit-sha.txt`, or packages whose filenames
  don't match the recorded version, fail `prepare-release` with an explicit
  message rather than releasing an unknown or mismatched version.
- `--skip-duplicate` makes a re-run after a partial NuGet push idempotent; a
  duplicate is surfaced as a warning so it can't masquerade as a fresh release.
- An existing tag on the same commit is a no-op; on a different commit it fails.
- Jobs are re-runnable from the Actions UI for 7 days, the retention of the
  intermediate `release-packages` artifact they consume.
- Release-note generation never blocks the release: it degrades to a commit list,
  and the degradation is reported in the summary table.

## Verification

Done:

- `actionlint` over both workflows — clean. Note this validates workflow and
  expression syntax only; it does not check that an action's referenced outputs
  exist, which is how the `nuGetVersion` bug below got as far as review.
- YAML parse check of `ci.yml`, `publish-release.yml`, `GitVersion.yml`.
- `dotnet pack` locally for both packable projects — both packages produced,
  named `Medino.3.0.10-dev-publish-pipeline.1.nupkg` (i.e. GitVersion's `semVer`).
- `dotnet-gitversion` on this branch — `MajorMinorPatch: 3.0.10`, confirming the
  `release/3.0.9` tag plus `tag-prefix` yields the intended next version.
- Enumerated GitVersion 6's actual output variables: there is no `NuGetVersion`,
  so the first draft's `steps.gitversion.outputs.nuGetVersion` would have
  resolved empty and failed the pack job on every push. Corrected to `semVer`.
- Reproduced the `next-version` parse failure and isolated it to `tag-prefix`.

Not provable locally:
- End-to-end behaviour (artifact hand-off, NuGet push, release creation) can only
  be proven by merging to `main` and running the workflows; the first real run is
  expected to use `skip_nuget: true, skip_tag: true` as a dry run.
- The release-notes job's `gh pr view` / `gh issue list` calls depend on the
  granted `pull-requests: read` / `issues: read` scopes; only a real run confirms
  the enrichment lands rather than being skipped.
