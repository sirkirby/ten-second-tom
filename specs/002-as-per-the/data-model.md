# Data Model: GitHub Actions CI/CD Pipeline

**Feature**: GitHub Actions CI/CD Pipeline  
**Date**: 2025-10-03  
**Status**: Complete

## Overview

This document defines the data structures and relationships for the CI/CD pipeline workflows. Unlike traditional application data models with databases, this feature deals with workflow configurations, build artifacts, and CI/CD metadata.

---

## Entity 1: Workflow Configuration

**Description**: YAML files defining GitHub Actions workflows

**Location**: `.github/workflows/`

**Attributes**:
- `name` (string): Human-readable workflow name
- `on` (object): Trigger events (pull_request, push, etc.)
- `jobs` (array): Collection of job definitions
- `concurrency` (object): Concurrency group and cancellation rules
- `env` (object): Environment variables available to all jobs

**Relationships**:
- Contains multiple Jobs
- References Secrets
- Produces WorkflowRuns

**Validation Rules**:
- Must be valid YAML syntax
- Must follow GitHub Actions schema
- Job names must be unique within workflow
- Required triggers must be present

**State**: Static configuration (version controlled)

**Example**:
```yaml
name: PR Validation
on:
  pull_request:
    branches: [main]
jobs:
  test:
    runs-on: ubuntu-latest
    steps: [...]
```

---

## Entity 2: Workflow Run

**Description**: A single execution instance of a workflow

**Source**: GitHub Actions runtime

**Attributes**:
- `run_id` (string, unique): GitHub-assigned run identifier
- `workflow_name` (string): Name of the workflow
- `trigger_event` (enum): pull_request | push | workflow_dispatch
- `status` (enum): queued | in_progress | completed
- `conclusion` (enum): success | failure | cancelled | skipped
- `started_at` (datetime): When run began
- `completed_at` (datetime, optional): When run finished
- `duration_seconds` (integer): Time from start to completion
- `commit_sha` (string): Git commit that triggered run
- `branch` (string): Branch associated with run
- `actor` (string): GitHub username who triggered run

**Relationships**:
- Belongs to one Workflow Configuration
- Contains multiple Job Runs
- May produce Build Artifacts
- May produce Test Results
- May produce Coverage Reports

**Lifecycle**:
1. `queued`: Waiting for available runner
2. `in_progress`: Jobs executing
3. `completed`: All jobs finished (check conclusion for outcome)

**Performance Constraints**:
- PR validation runs: ≤10 minutes (FR-029, NFR-001)
- Main branch build runs: ≤15 minutes (NFR-003)
- Release runs: ≤30 minutes (NFR-004)

---

## Entity 3: Job Run

**Description**: A single job execution within a workflow run

**Source**: GitHub Actions runtime

**Attributes**:
- `job_id` (string, unique): GitHub-assigned job identifier
- `job_name` (string): Name from workflow configuration
- `runs_on` (string): Runner environment (ubuntu-latest, etc.)
- `status` (enum): queued | in_progress | completed
- `conclusion` (enum): success | failure | cancelled | skipped
- `started_at` (datetime): When job began
- `completed_at` (datetime, optional): When job finished
- `steps` (array): Collection of step results

**Relationships**:
- Belongs to one Workflow Run
- Contains multiple Steps
- May use Secrets
- May produce Artifacts

**Dependencies**:
- May depend on other jobs (needs: [job1, job2])
- Execution order determined by dependency graph

---

## Entity 4: Build Artifact

**Description**: Compiled executable for a specific platform

**Location**: GitHub Actions artifacts storage, GitHub Releases

**Attributes**:
- `artifact_id` (string, unique): GitHub artifact identifier
- `platform` (enum): osx-x64 | osx-arm64 | win-x64
- `version` (string): Semantic version (e.g., "1.2.3")
- `commit_sha` (string): Git commit that produced artifact
- `build_timestamp` (datetime): When artifact was built
- `file_name` (string): Name of executable file
- `file_size_bytes` (integer): Size of file in bytes
- `checksum_sha256` (string): SHA256 hash for verification
- `runtime_identifier` (string): .NET RID (e.g., "osx-x64")

**Relationships**:
- Produced by one Build Job Run
- May be included in Package Release
- May be attached to GitHub Release

**Validation Rules**:
- File size must be <50MB (NFR-018)
- Version must follow semver format (FR-022)
- Checksum must be verifiable
- Must be self-contained (all dependencies included, FR-017)

**Verification Steps**:
1. Executable exists and is non-empty
2. File is executable (Unix permissions or .exe extension)
3. Smoke test: executable runs with `--version` or `--help`

**Retention**: 90 days in Actions artifacts, indefinite in Releases

---

## Entity 5: Test Result

**Description**: Outcome of running the test suite

**Source**: `dotnet test` output

**Attributes**:
- `test_run_id` (string): Identifier for this test execution
- `total_tests` (integer): Number of tests executed
- `passed_tests` (integer): Number of tests that passed
- `failed_tests` (integer): Number of tests that failed
- `skipped_tests` (integer): Number of tests skipped
- `execution_time_seconds` (decimal): Total test execution time
- `test_framework` (string): "xUnit" (fixed per constitution)
- `failures` (array): Collection of failure details

**Failure Detail**:
- `test_name` (string): Fully qualified test name
- `error_message` (string): Assertion or exception message
- `stack_trace` (string): Execution stack trace
- `test_file` (string): Source file containing test

**Relationships**:
- Produced by one Test Job Run
- Associated with one Commit SHA
- May trigger Coverage Report generation

**Validation Rules**:
- `failed_tests` must equal 0 for PR merge (FR-004, FR-026)
- `passed_tests` must be >0 (at least some tests ran)

**Performance Constraints**:
- Unit tests: ≤3 minutes
- Integration tests: ≤2 minutes
- Total: ≤5 minutes (NFR-001)

---

## Entity 6: Coverage Report

**Description**: Code coverage analysis results

**Source**: Coverlet + ReportGenerator

**Attributes**:
- `coverage_id` (string): Identifier for this coverage report
- `commit_sha` (string): Git commit analyzed
- `branch` (string): Branch analyzed
- `line_coverage_percent` (decimal): Overall line coverage percentage
- `branch_coverage_percent` (decimal): Branch coverage percentage
- `total_lines` (integer): Total lines of code
- `covered_lines` (integer): Lines executed by tests
- `uncovered_lines` (integer): Lines not executed
- `generated_at` (datetime): When report was generated
- `file_coverage` (array): Per-file breakdown

**File Coverage Detail**:
- `file_path` (string): Source file path
- `line_coverage_percent` (decimal): Coverage for this file
- `covered_lines` (integer): Covered lines in file
- `uncovered_lines` (integer): Uncovered lines in file
- `methods` (array): Per-method coverage

**Relationships**:
- Produced by one Coverage Job Run
- Associated with one Test Result
- May be compared to baseline for diff

**Validation Rules**:
- `line_coverage_percent` must be ≥80% for PR merge (FR-009, FR-027)
- Report must be in Cobertura format for tooling compatibility

**Diff Calculation**:
- Compare `line_coverage_percent` to baseline from target branch
- If absolute difference ≥5%, post comment on PR (FR-012)
- Report shows: current %, previous %, delta %, files with largest changes

---

## Entity 7: Package Release

**Description**: Published package in a package manager

**Location**: Homebrew tap, winget-pkgs repo, chocolatey.org

**Attributes**:
- `release_id` (string): Unique identifier for this release
- `package_manager` (enum): homebrew | winget | chocolatey
- `version` (string): Semantic version
- `release_date` (datetime): When package was published
- `status` (enum): pending | published | failed
- `package_url` (string): Installation source URL
- `approval_status` (enum): not_required | pending | approved | rejected
- `approved_by` (string, optional): GitHub username of approver
- `publication_logs` (string): Output from publication process

**Relationships**:
- References multiple Build Artifacts
- Created by one Release Workflow Run
- May require approval from CODEOWNERS

**Validation Rules**:
- Version must not already exist (FR-023)
- Version must follow semantic versioning (FR-022)
- All required Build Artifacts must be available
- Checksums must match Build Artifacts

**Publication Process**:

**Homebrew**:
1. Generate formula with artifact URLs and checksums
2. Push to tap repository
3. Status becomes "published" immediately

**Winget**:
1. Generate manifest YAML
2. Submit PR to microsoft/winget-pkgs
3. Status remains "pending" until PR merged
4. Requires approval from CODEOWNERS (FR-025, NFR-010)

**Chocolatey**:
1. Generate nuspec and installation scripts
2. Push to chocolatey.org via API
3. Status "pending" during moderation
4. Requires approval from CODEOWNERS (FR-025, NFR-010)
5. Status becomes "published" after moderation

**Retry Logic**:
- Failed publications retry up to 3 times with exponential backoff
- Permanent failures notify via PR comment (FR-031)

---

## Entity 8: GitHub Secret

**Description**: Encrypted credential for workflow authentication

**Location**: Repository settings (not version controlled)

**Attributes**:
- `secret_name` (string): Reference name in workflows
- `scope` (enum): repository | environment
- `environment` (string, optional): Environment name if scoped
- `required_by` (array): Workflows/jobs that require this secret

**Required Secrets**:

| Secret Name | Scope | Purpose | Required For |
|-------------|-------|---------|--------------|
| `GITHUB_TOKEN` | repository (auto) | Homebrew, Releases, PR comments | All workflows |
| `HOMEBREW_TAP_TOKEN` | repository | Push to tap repo | release.yml |
| `WINGET_TOKEN` | environment (production) | Submit winget PRs | release.yml |
| `CHOCOLATEY_API_KEY` | environment (production) | Push to chocolatey.org | release.yml |

**Validation Rules**:
- Secrets must never be logged or exposed (NFR-008)
- Production-scoped secrets require environment approval

**Security Requirements**:
- Rotate annually
- Use least-privilege scopes
- Document in README without revealing values

---

## Entity 9: CODEOWNERS Entry

**Description**: File defining approval requirements for releases

**Location**: `.github/CODEOWNERS`

**Format**:
```
# Release workflows require approval
/.github/workflows/release.yml @sirkirby
```

**Attributes**:
- `pattern` (string): File glob pattern
- `owners` (array): GitHub usernames or team slugs

**Relationships**:
- Referenced by GitHub Environment protection rules
- Enforces approval on Package Release jobs

**Validation Rules**:
- At least one owner must approve before release jobs execute (FR-025, NFR-010)
- Owners must have write access to repository

---

## Workflow Data Flow

### Pull Request Validation Flow

```
Developer pushes to PR
  ↓
PR Validation Workflow Run created
  ↓
Build Job Run → compiles code, checks warnings
  ↓
Test Job Run → executes tests → produces Test Result
  ↓
Coverage Job Run → calculates coverage → produces Coverage Report
  ↓
Coverage compared to baseline
  ↓
If coverage change ≥5% → post PR comment
If coverage <80% → fail check, block merge
If tests fail → fail check, block merge
  ↓
Validate Job Run → aggregates status → reports to PR
```

### Main Branch Build Flow

```
PR merged to main
  ↓
Build Workflow Run created
  ↓
Test Job Run → re-run tests → produces Test Result
  ↓
Build Matrix Jobs (parallel) → produce Build Artifacts for each platform
  ↓
Verify Job Run → smoke tests each artifact
  ↓
Upload Artifacts Job → stores artifacts for 90 days
```

### Release Flow

```
Developer pushes tag v1.2.3
  ↓
Release Workflow Run created
  ↓
Validate Version Job → checks semver format, uniqueness
  ↓
Build Release Jobs (parallel) → produce Build Artifacts
  ↓
Create GitHub Release Job → attaches artifacts → creates release
  ↓
Publish Homebrew Job → updates tap → Package Release (homebrew)
  ↓
[APPROVAL GATE - CODEOWNERS]
  ↓
Publish Winget Job → submits PR → Package Release (winget, pending)
  ↓
[APPROVAL GATE - CODEOWNERS]
  ↓
Publish Chocolatey Job → pushes package → Package Release (chocolatey, pending)
```

---

## Entity Relationship Diagram (Text)

```
Workflow Configuration (1) --produces--> (M) Workflow Run
Workflow Run (1) --contains--> (M) Job Run
Job Run (1) --produces--> (0..1) Test Result
Job Run (1) --produces--> (0..1) Coverage Report
Job Run (1) --produces--> (0..M) Build Artifact
Build Artifact (M) --included-in--> (1) Package Release
Workflow Run (1) --references--> (M) GitHub Secret
Package Release (1) --requires-approval-from--> (M) CODEOWNERS Entry
Coverage Report (1) --compared-to--> (1) Coverage Report (baseline)
Test Result (1) --triggers--> (1) Coverage Report
```

---

## Performance Optimization Considerations

### Caching Strategy

**NuGet Packages**:
- Cache key: `hashFiles('**/packages.lock.json')`
- Reduces restore time from ~30s to ~5s

**Build Outputs**:
- Cache key: `runner.os-hashFiles('**/*.csproj')-github.sha`
- Reduces incremental build time

**Coverage Baseline**:
- Cache key: `coverage-github.base_ref-base.sha`
- Enables accurate coverage diff

### Parallel Execution

**Build Matrix**:
- All platform builds run in parallel
- No dependencies between platform jobs

**Test Execution**:
- Unit and integration tests can run in parallel (future optimization)

---

## Monitoring and Observability

### Logged Data (FR-034)

All workflows log:
- Workflow run ID and trigger
- Job start/completion times
- Command outputs (sanitized for secrets)
- Error messages and stack traces
- Artifact checksums and metadata

### Metrics Tracked

- Build success rate over time (FR-032)
- Average execution time per workflow
- Coverage trends over time
- Artifact sizes per platform

### Failure Notifications (FR-031)

- Posted as PR comments for release failures
- Include workflow run URL and error excerpt

---

## Acceptance Criteria Mapping

✅ **Test Results** satisfy FR-001 through FR-005  
✅ **Coverage Reports** satisfy FR-006 through FR-012  
✅ **Build Artifacts** satisfy FR-013 through FR-018  
✅ **Package Releases** satisfy FR-019 through FR-025  
✅ **Workflow data flow** satisfies FR-026 through FR-034  
✅ **Performance constraints** satisfy NFR-001 through NFR-004  
✅ **Security model** satisfies NFR-008 through NFR-010  

---

## Conclusion

This data model defines all entities and relationships required for the CI/CD pipeline. The model focuses on workflow configurations, runtime executions, and artifacts rather than traditional application data. All entities have clear validation rules, relationships, and lifecycle states that map directly to functional requirements.

**Status**: Ready for contract generation (Phase 1 next step).
