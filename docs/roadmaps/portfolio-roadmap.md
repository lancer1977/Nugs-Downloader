# Nugs-Downloader portfolio roadmap

## 90-day evidence snapshot
- Commits (90 days): 5
- Files changed (90 days): 145
- Last signal: 68cf1f0 (4 days ago)
- Top modified areas: csharp(61);src(21);ui(19);docs(18);pkg(11);ui_embedded.go(1)
- Notes: clean_at_scan

## Current repo posture
- Stack: .NET
- Docs folder: yes
- Roadmap folder: yes
- Features docs: yes
- Tests indexed: yes

## Discovery
- [x] Capture and timestamp recent change signal
- [x] Capture top-level area concentration
- [x] Document owner and intent for area: csharp(61)
- [x] Add explicit release gates for next validation steps

## Area Notes
- `csharp(61)` is the active C# application/runtime area under `csharp/`; keep next slices focused on the Blazor host, provider adapters, workflow logic, and regression coverage.

## Release Gates
- `dotnet build csharp/NugsDownloader.sln --no-restore`
- `dotnet test csharp/tests/NugsDownloader.Tests/NugsDownloader.Tests.csproj --filter LivePhishMediaProviderTests`
- `dotnet test csharp/tests/NugsDownloader.Tests/NugsDownloader.Tests.csproj --no-restore`
- `dotnet run --project csharp/NugsDownloader.Web`

## V1 (stability)
- [x] Close gaps in docs and feature notes for recently touched areas
- [x] Add or update smoke checks for changed source paths
- [x] Validate packaging and deploy assumptions where infra/config changed

## V2 (confidence)
- [x] Add deeper tests on highest-churn areas
- [x] Expand runbooks for recurring operator or publishing workflows
- [x] Standardize naming and checklist structure for future items

## V10 (scale)
- [x] Move to a stable platform pattern with cross-repo checklist templates
- [x] Split roadmap into discrete feature-level and initiative-level folders
- [x] Define long-range acceptance criteria with operational and product owners

## Top touched files (90-day top 10)
- .codex
- .github/workflows/ci.yml
- .gitignore
- Makefile
- README.md
- ... and 5 more

## Follow-up ideas
- [ ] Convert area signals into one short feature roadmap within docs/features
- [ ] Add changelog notes in docs for behavior-impacting updates
- [ ] Add simple owner checklist for release readiness
