# Update dev-cli workflow to publish the library

## Description

Extend the dev-cli `workflow` command and GitHub Actions pipeline so `TimeWarp.Components` is published to NuGet.org on release. The workflow currently runs only `clean → build → test` with no mode detection or publish step, and the GitHub Actions workflow invokes `build`/`test` directly instead of `workflow`.

## Requirements

- PR/merge runs validate the package without publishing
- Release runs publish `TimeWarp.Components` to NuGet.org via OIDC Trusted Publishing
- Version in `source/Directory.Build.props` must match the git tag (`check-version` already delegates to `ganda repo check-version`)
- `.nupkg` artifacts land in `artifacts/packages/` for CI upload

## Checklist

- [ ] Add MSBuild package output configuration
  - [ ] `GeneratePackageOnBuild=true` (root or `source/Directory.Build.props`)
  - [ ] `PackageOutputPath` → `artifacts/packages/`
- [ ] Update `workflow-command.cs` with CI mode detection (mirror `timewarp-amuru`)
  - [ ] `--mode` option and auto-detect from `GITHUB_EVENT_NAME`
  - [ ] PR/merge pipeline: `clean → build → verify-samples → test → check-version`
  - [ ] Release pipeline: `clean → build → push` (NuGet)
  - [ ] `--api-key` option for NuGet push (from OIDC login step)
- [ ] Implement `PushPackageAsync` for `TimeWarp.Components.{version}.nupkg`
- [ ] Update `.github/workflows/workflow.yml`
  - [ ] Single job calling `dotnet run tools/dev-cli/dev.cs -- workflow`
  - [ ] `release` and `workflow_dispatch` triggers
  - [ ] `nuget/login@v1` with `id-token: write` on release
  - [ ] Pass `--api-key` from OIDC step on release events
  - [ ] Upload `artifacts/packages/*.nupkg` as workflow artifacts
- [ ] Test locally: `dev workflow` (PR mode) completes successfully
- [ ] Verify release path with a dry-run or test push against a local feed

## Notes

**Current state:**
- `tools/dev-cli/endpoints/workflow-command.cs` — `clean → build → test` only
- `.github/workflows/workflow.yml` — calls `build` and `test` directly, no release trigger
- `source/timewarp-components/timewarp-components.csproj` — `PackageId` is `TimeWarp.Components`, version in `source/Directory.Build.props` (`1.0.0-beta.1`)
- `check-version` command exists and delegates to `ganda repo check-version --strategy git-tag`

**Reference implementation:**
- `timewarp-amuru/tools/dev-cli/endpoints/workflow-command.cs` — mode detection, `PushPackageAsync`, release pipeline
- `timewarp-amuru/.github/workflows/workflow.yml` — OIDC NuGet login + single `workflow` invocation

**NuGet publisher:** `TimeWarp.Enterprises` (same as other TimeWarp repos)