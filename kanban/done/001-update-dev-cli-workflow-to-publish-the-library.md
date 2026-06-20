# Update dev-cli workflow to publish the library

## Description

Extend the dev-cli `workflow` command and GitHub Actions pipeline so `TimeWarp.Components` is published to NuGet.org on release. The workflow currently runs only `clean → build → test` with no mode detection or publish step, and the GitHub Actions workflow invokes `build`/`test` directly instead of `workflow`.

## Requirements

- PR/merge runs validate the package without publishing
- Release runs publish `TimeWarp.Components` to NuGet.org via OIDC Trusted Publishing
- Version in `source/Directory.Build.props` must match the git tag (`check-version` already delegates to `ganda repo check-version`)
- `.nupkg` artifacts land in `artifacts/packages/` for CI upload

## Checklist

- [x] Add MSBuild package output configuration
  - [x] `GeneratePackageOnBuild=true` (root or `source/Directory.Build.props`)
  - [x] `PackageOutputPath` → `artifacts/packages/`
- [x] Update `workflow-command.cs` with CI mode detection (mirror `timewarp-amuru`)
  - [x] `--mode` option and auto-detect from `GITHUB_EVENT_NAME`
  - [x] PR/merge pipeline: `clean → build → verify-samples → test → check-version`
  - [x] Release pipeline: `clean → build → push` (NuGet)
  - [x] `--api-key` option for NuGet push (from OIDC login step)
- [x] Implement `PushPackageAsync` for `TimeWarp.Components.{version}.nupkg`
- [x] Update `.github/workflows/workflow.yml`
  - [x] Single job calling `dotnet run tools/dev-cli/dev.cs -- workflow`
  - [x] `release` and `workflow_dispatch` triggers
  - [x] `nuget/login@v1` with `id-token: write` on release
  - [x] Pass `--api-key` from OIDC step on release events
  - [x] Upload `artifacts/packages/*.nupkg` as workflow artifacts
- [x] Test locally: `dev workflow` (PR mode) completes successfully
- [x] Verify release path with a dry-run or test push against a local feed

## Notes

**Reference implementation:**
- `timewarp-amuru/tools/dev-cli/endpoints/workflow-command.cs` — mode detection, `PushPackageAsync`, release pipeline
- `timewarp-amuru/.github/workflows/workflow.yml` — OIDC NuGet login + single `workflow` invocation
- `timewarp-terminal/tools/dev-cli/endpoints/workflow.cs` — alternate pattern (see Results)

**NuGet publisher:** `TimeWarp.Enterprises` (same as other TimeWarp repos)

## Results

Implemented following the **timewarp-amuru** pattern (handler-based sub-commands, `GeneratePackageOnBuild`, explicit `--mode` flag).

**Pattern divergence vs timewarp-terminal** (GitHub workflows are aligned; dev-cli workflow differs):
| Aspect | timewarp-amuru | timewarp-terminal |
|--------|----------------|-------------------|
| Mode detection | `--mode` flag + `GITHUB_EVENT_NAME` | `GITHUB_EVENT_NAME` + `--api-key` presence only |
| PR pipeline | includes `check-version` | no `check-version` |
| Release pipeline | `clean → build → push` (pack on build) | `clean → build → check-version → pack → push` |
| check-version | `ganda repo check-version` in PR | HTTP NuGet.org check in release |
| Implementation | sub-command handlers | inline `dotnet` shell calls |

**Verification:**
- `dev workflow --mode pr` — passed (5 tests, check-version OK)
- `dev workflow --mode release` — builds `artifacts/packages/TimeWarp.Components.1.0.0-beta.1.nupkg`, push fails with 401 without API key (expected)