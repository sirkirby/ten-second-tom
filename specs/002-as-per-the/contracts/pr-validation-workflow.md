# Contract: PR Validation Workflow

**File**: `.github/workflows/pr-validation.yml`  
**Purpose**: Validate pull requests with tests, coverage, and quality gates  
**Trigger**: Pull requests targeting `main` branch

---

## Workflow Interface

### Inputs
- **Event**: `pull_request` (opened, synchronize, reopened)
- **Target Branch**: `main`
- **Source**: Any feature branch

### Outputs
- **Test Results**: Pass/fail status with detailed failure information
- **Coverage Percentage**: Current coverage with comparison to baseline
- **Coverage Diff**: Percentage point change from target branch
- **Quality Status**: Pass/fail for compiler warnings check

### Exit Conditions
- **Success**: All jobs pass (tests pass, coverage ≥80%, no warnings)
- **Failure**: Any job fails (tests fail, coverage <80%, warnings present)

---

## Jobs

### Job 1: Build

**Purpose**: Compile code and check for compiler warnings

**Runner**: `ubuntu-latest`

**Steps**:
1. Checkout code
2. Setup .NET 9 SDK with NuGet caching
3. Restore dependencies
4. Build with Release configuration
5. Fail if any compiler warnings detected

**Inputs**:
- Source code from PR branch
- .NET 9 SDK
- NuGet packages

**Outputs**:
- Compiled binaries (not uploaded, used by subsequent jobs)
- Build status (success/failure)

**Performance Target**: ≤2 minutes

**Contract**:
```yaml
Preconditions:
  - Valid C# code in src/
  - Valid .csproj file
  - Restorable NuGet dependencies

Postconditions:
  - Code compiles without errors
  - Zero compiler warnings
  - Binaries available in obj/ and bin/

Failure Modes:
  - Compilation errors → fail with error messages
  - Compiler warnings → fail with warning messages
  - Missing dependencies → fail with restore errors
```

---

### Job 2: Test

**Purpose**: Execute unit and integration tests

**Runner**: `ubuntu-latest`

**Dependencies**: Job 1 (Build)

**Steps**:
1. Run `dotnet test` with xUnit
2. Generate test results in TRX format
3. Upload test results as artifact
4. Fail if any tests fail

**Inputs**:
- Compiled binaries from Build job
- Test projects in tests/

**Outputs**:
- Test result summary (passed, failed, skipped counts)
- Test result artifact (TRX file)
- Individual failure details if any

**Performance Target**: ≤5 minutes (NFR-001)

**Contract**:
```yaml
Preconditions:
  - Build job succeeded
  - Test projects exist and compile
  - xUnit test framework available

Postconditions:
  - All tests executed
  - Test results recorded
  - failed_tests == 0 for success

Failure Modes:
  - Test failures → fail with test names, messages, stack traces
  - Test timeouts → fail with timeout message
  - Test runner crashes → fail with error details
```

---

### Job 3: Coverage

**Purpose**: Calculate code coverage and enforce 80% threshold

**Runner**: `ubuntu-latest`

**Dependencies**: Job 1 (Build)

**Steps**:
1. Run `dotnet test` with coverlet collector
2. Generate coverage report with ReportGenerator
3. Parse coverage percentage from Cobertura XML
4. Retrieve baseline coverage from cache (target branch)
5. Calculate coverage diff
6. If diff ≥5%, post PR comment with details
7. If coverage <80%, fail job
8. Upload coverage report as artifact

**Inputs**:
- Compiled binaries from Build job
- Test projects
- Baseline coverage from target branch (if available)

**Outputs**:
- Coverage percentage (decimal, e.g., 0.8245)
- Coverage diff (percentage points, e.g., +2.5%)
- Coverage report HTML
- Coverage report Cobertura XML
- PR comment (if threshold met for commenting)

**Performance Target**: ≤3 minutes (including ≤2 min for calculation per NFR-002)

**Contract**:
```yaml
Preconditions:
  - Build job succeeded
  - Coverlet available
  - ReportGenerator available
  - Tests can run

Postconditions:
  - Coverage calculated
  - Coverage percentage >= 0.80 for success
  - Coverage report generated
  - PR comment posted if |diff| >= 0.05

Failure Modes:
  - Coverage <80% → fail with current percentage and message
  - Coverage calculation error → fail with diagnostic info
  - Report generation error → fail with error message
  
Success Criteria:
  - line_coverage_percent >= 0.80
  - All files analyzed
  - Report files exist
```

---

### Job 4: Validate

**Purpose**: Aggregate status from all jobs and report to PR

**Runner**: `ubuntu-latest`

**Dependencies**: Jobs 1, 2, 3 (Build, Test, Coverage)

**Steps**:
1. Check status of all dependent jobs
2. Summarize results
3. Set overall workflow status

**Inputs**:
- Status from Build job
- Status from Test job
- Status from Coverage job

**Outputs**:
- Overall pass/fail status
- Summary comment (optional)

**Performance Target**: <1 minute

**Contract**:
```yaml
Preconditions:
  - All dependent jobs completed

Postconditions:
  - Workflow status reflects aggregate of all jobs
  - PR checks updated

Success Criteria:
  - Build: success
  - Test: success (zero failures)
  - Coverage: success (>=80%)

Failure Modes:
  - Any dependent job failed → this job fails
  - Reports aggregate failure reason
```

---

## Workflow-Level Contracts

### Performance Contract (FR-029, NFR-001)

```yaml
Total Execution Time: ≤10 minutes

Breakdown:
  - Checkout & Setup: ≤1 minute
  - Build: ≤2 minutes
  - Test: ≤5 minutes
  - Coverage: ≤3 minutes (parallel with Test conceptually, but runs after)
  - Validate: <1 minute

Mitigation if exceeded:
  - Implement test parallelization
  - Increase caching effectiveness
  - Split unit/integration tests into parallel jobs
```

### Quality Contract (FR-026, FR-027, FR-028)

```yaml
Quality Gates:
  - Zero compiler warnings (FR-028)
  - Zero test failures (FR-026)
  - Coverage >= 80% (FR-027)

Enforcement:
  - Configure as required status check in branch protection
  - PR cannot merge unless this workflow succeeds
  - Manual override requires admin privileges
```

### Notification Contract (FR-012)

```yaml
Coverage Comment Posted When:
  - |coverage_diff| >= 0.05 (5 percentage points)
  
Comment Content:
  - Current coverage percentage
  - Previous coverage percentage
  - Diff (+X.X% or -X.X%)
  - Link to detailed report
  - Files with largest coverage changes (top 5)

Comment Format:
  ## 📊 Coverage Report
  
  **Current**: 82.5% | **Previous**: 80.0% | **Change**: +2.5% ✅
  
  [View Full Report](link)
  
  ### Top Changes
  - `src/Features/NewFeature/Handler.cs`: 0% → 100% (+100%)
  - ...
```

---

## Integration Points

### GitHub API
- **POST** `/repos/{owner}/{repo}/issues/{pr_number}/comments`: Post coverage comment
- **GET** `/repos/{owner}/{repo}/pulls/{pr_number}`: Get PR details (base ref)

### GitHub Actions Artifacts
- **Upload**: Test results (TRX), Coverage reports (HTML, XML)
- **Retention**: 30 days

### GitHub Actions Cache
- **Store**: NuGet packages, baseline coverage
- **Keys**: `packages-{hash}`, `coverage-{base_ref}-{base_sha}`

---

## Error Scenarios

| Scenario | Detection | Response | User Experience |
|----------|-----------|----------|-----------------|
| Test failures | `failed_tests > 0` | Fail job, show details in logs | PR check fails with "X tests failed", link to details |
| Coverage below threshold | `coverage < 0.80` | Fail job, show percentage | PR check fails with "Coverage 75% (required 80%)" |
| Compilation errors | dotnet build exit code != 0 | Fail job, show errors | PR check fails with compiler error messages |
| Compiler warnings | dotnet build output contains warnings | Fail job, show warnings | PR check fails with "1 warning found" |
| Baseline coverage unavailable | Cache miss on first PR from new branch | Use 0% as baseline, post comment | PR comment notes baseline not available |
| Coverage calculation failure | coverlet/ReportGenerator error | Fail job, show error | PR check fails with diagnostic message |

---

## Testing Strategy

**Contract Tests** (to be created in Phase 1):
- Mock PR event triggers workflow
- Verify job execution order (Build → Test/Coverage → Validate)
- Verify failure propagation (failed job → failed workflow)
- Verify status check registration

**Validation Tests** (manual during Phase 5):
- Create PR with passing tests → workflow succeeds
- Create PR with failing test → workflow fails, shows failure details
- Create PR reducing coverage to 75% → workflow fails with coverage message
- Create PR increasing coverage by 6% → workflow posts comment
- Create PR with compiler warning → workflow fails with warning message

---

## Acceptance Criteria from Spec

✅ **FR-001**: Automatically runs all unit tests on every PR  
✅ **FR-002**: Automatically runs all integration tests on every PR  
✅ **FR-003**: Reports test results with pass/fail status on PR  
✅ **FR-004**: Fails and blocks merge if any tests fail  
✅ **FR-005**: Displays test failure details with errors and stack traces  
✅ **FR-006**: Calculates code coverage percentage for every PR  
✅ **FR-007**: Displays coverage percentage prominently on PR  
✅ **FR-008**: Shows coverage diff comparing PR to target branch  
✅ **FR-009**: Fails and blocks merge if coverage falls below 80%  
✅ **FR-010**: Generates detailed coverage report with covered/uncovered lines  
✅ **FR-011**: Highlights files and methods with insufficient coverage  
✅ **FR-012**: Comments on PR when coverage changes by ≥5%  
✅ **FR-026**: Prevents merging PRs that fail tests  
✅ **FR-027**: Prevents merging PRs that reduce coverage below 80%  
✅ **FR-028**: Prevents merging PRs with compiler warnings  
✅ **FR-029**: Runs validation checks within 10 minutes for typical PRs  
✅ **FR-030**: Caches dependencies to improve build performance  
✅ **NFR-001**: Test suite completes within 5 minutes  
✅ **NFR-002**: Coverage calculation completes within 2 minutes  

---

## Implementation Notes

- Use `actions/checkout@v4` for code checkout
- Use `actions/setup-dotnet@v4` for .NET 9 SDK setup with caching
- Use `danielpalme/ReportGenerator-GitHub-Action@5` for coverage reports
- Use `actions/upload-artifact@v4` for artifact storage
- Use `actions/github-script@v7` for PR comment posting
- Pin all action versions for security and reproducibility
- Use `concurrency` group `pr-${{ github.event.pull_request.number }}` to cancel outdated runs

---

**Status**: Contract complete, ready for implementation in Phase 4.
