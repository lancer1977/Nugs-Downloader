# Scripts

## `nugs-watchdog.sh`

LAN-only production watchdog for the live internal Nugs service. It is quiet on success and prints only secret-free failure details. It verifies health/readiness, Blazor static assets, private DNS posture, public DNS absence, Swarm replica state, and the downloads free-space threshold when Docker is available.

```bash
scripts/nugs-watchdog.sh
```

Override defaults with `NUGS_WATCHDOG_*` environment variables. The script never needs provider credentials. Set `NUGS_WATCHDOG_REQUIRE_DOCKER=1` when running on the Swarm manager so missing service/free-space checks fail hard.

## `validate-ci-policy.py` and `test-ci-policy.sh`

Deterministic, offline verification for the GitHub Actions permission boundary:

```bash
scripts/test-ci-policy.sh
```

The structural verifier proves that the `test` and `container` validation jobs
are read-only, while the only `packages: write` grant belongs to the `publish`
job. It rejects YAML aliases, merges, flow-style permission declarations, and
unrecognized permission values so those cannot bypass the policy. The test
wrapper also runs committed negative fixtures for top-level, test-job, and
container-job package writes. The publish job runs only for trusted pushes to
`main` or `master`; pull requests build and validate without package-publication
authority.

## Coverage template

This folder also captures a reusable shell-script baseline for .NET coverage workflows.

Copy the script into a product repo when you want a simple local command that:

- runs the target test project or solution
- collects `XPlat Code Coverage`
- writes artifacts to a predictable `TestResults/` location
- optionally generates a human-readable HTML report when ReportGenerator is available

## Expected inputs

- `COVERAGE_TARGET` or the first positional argument: the solution or test project path
- `CONFIGURATION` (optional): defaults to `Release`
- `COVERAGE_RESULTS_DIR` (optional): defaults to `./TestResults/coverage`

## Notes

- The script is intentionally lightweight so repos can copy it and adapt the target path.
- If ReportGenerator is installed, the script will emit an HTML summary under the report directory.
- Repos with more advanced needs can wrap this command in CI or a repo-local task file.
