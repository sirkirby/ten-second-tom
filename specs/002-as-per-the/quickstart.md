# Quickstart: GitHub Actions CI/CD Pipeline

**Feature**: GitHub Actions CI/CD Pipeline  
**Date**: 2025-10-03  
**Purpose**: Manual validation guide for CI/CD workflows

---

## Prerequisites

- Repository with GitHub Actions enabled
- Write access to repository (for creating branches and PRs)
- GitHub secrets configured (for release workflow)
- .NET 9 SDK installed locally (for local validation)

---

## Scenario 1: PR Validation Workflow

**Goal**: Verify that PR validation workflow runs tests, checks coverage, and enforces quality gates

### Steps

1. **Create a feature branch**:
   ```bash
   git checkout -b test/pr-validation
   ```

2. **Make a trivial change** (to trigger workflow):
   ```bash
   echo "// Test change" >> src/Program.cs
   git add src/Program.cs
   git commit -m "test: trigger PR validation"
   git push origin test/pr-validation
   ```

3. **Create a Pull Request**:
   - Go to GitHub repository
   - Click "Pull requests" → "New pull request"
   - Select `test/pr-validation` → `main`
   - Create PR

4. **Observe workflow execution**:
   - Navigate to "Actions" tab
   - Find "PR Validation" workflow run
   - Verify workflow is running

### Expected Results

✅ **Build job completes** in ~2 minutes:
- Compiles code successfully
- No compiler warnings
- Status: Success (green checkmark)

✅ **Test job completes** in ~3-5 minutes:
- Runs all unit and integration tests
- All tests pass
- Test count displayed in logs
- Status: Success (green checkmark)

✅ **Coverage job completes** in ~2-3 minutes:
- Calculates code coverage
- Coverage ≥80% (or job fails)
- Coverage report uploaded as artifact
- If coverage change ≥5%, PR comment posted
- Status: Success if ≥80%, Failure otherwise

✅ **Validate job completes** in <1 minute:
- Aggregates all job statuses
- Status: Success if all jobs passed

✅ **PR status check**:
- Green checkmark next to "PR Validation" check
- "Merge" button enabled (if all checks pass)

### Failure Test Cases

**Test Case A: Failing Test**

1. Add a failing test:
   ```csharp
   [Fact]
   public void This_Test_Should_Fail() => Assert.True(false);
   ```

2. Commit and push

3. **Expected**: Test job fails, shows failure details, PR blocked

**Test Case B: Low Coverage**

1. Add uncovered code without tests
2. Commit and push
3. **Expected**: Coverage job fails with percentage shown, PR blocked

**Test Case C: Compiler Warning**

1. Add code that generates a warning (e.g., unused variable)
2. Commit and push
3. **Expected**: Build job fails, shows warning, PR blocked

---

## Scenario 2: Main Branch Build Workflow

**Goal**: Verify that merging to main triggers build workflow and produces artifacts

### Steps

1. **Merge the PR** from Scenario 1 (after it passes):
   - Click "Merge pull request"
   - Confirm merge

2. **Observe build workflow**:
   - Navigate to "Actions" tab
   - Find "Build" workflow run (triggered by push to main)

3. **Verify workflow execution**:
   - Test job re-runs tests
   - Build matrix jobs run in parallel for each platform:
     - macOS x64
     - macOS ARM64
     - Windows x64
   - Verify job smoke-tests each executable
   - Upload job stores artifacts

### Expected Results

✅ **Test job completes** in ~3-5 minutes:
- All tests pass on main branch

✅ **Build jobs complete** in parallel (~10-15 minutes total):
- Three separate jobs for three platforms
- Each produces self-contained executable
- Each executable is <50MB

✅ **Verify jobs complete** in ~1 minute per platform:
- Each job runs smoke test (`--version` or `--help`)
- Confirms executable runs successfully

✅ **Artifacts uploaded**:
- Navigate to workflow run summary
- See three artifacts:
  - `ten-second-tom-osx-x64`
  - `ten-second-tom-osx-arm64`
  - `ten-second-tom-win-x64`
- Download and verify each is a valid executable

### Manual Verification

Download artifacts and test locally:

**macOS**:
```bash
# Download osx-x64 or osx-arm64 artifact
unzip ten-second-tom-osx-*.zip
chmod +x ten-second-tom
./ten-second-tom --version
# Should print version number
```

**Windows**:
```powershell
# Download win-x64 artifact
Expand-Archive ten-second-tom-win-x64.zip
.\ten-second-tom\ten-second-tom.exe --version
# Should print version number
```

---

## Scenario 3: Release Workflow (Homebrew Only - Phase 1)

**Goal**: Verify that pushing a version tag triggers release workflow and publishes to Homebrew

### Prerequisites

- `HOMEBREW_TAP_TOKEN` secret configured
- Homebrew tap repository created (e.g., `sirkirby/homebrew-ten-second-tom`)
- CODEOWNERS file exists with release approvers

### Steps

1. **Tag a release**:
   ```bash
   git checkout main
   git pull origin main
   git tag v0.1.0-test
   git push origin v0.1.0-test
   ```

2. **Observe release workflow**:
   - Navigate to "Actions" tab
   - Find "Release" workflow run (triggered by tag push)

3. **Verify workflow stages**:
   - Validate Version job checks semver format
   - Build Release jobs run in parallel for all platforms
   - Create Release job creates GitHub release

4. **Approve Homebrew publication** (if using environment protection):
   - Workflow pauses at "Publish Homebrew" job
   - Reviewer listed in CODEOWNERS receives notification
   - Reviewer clicks "Review deployments" and approves
   - Job continues

5. **Verify Homebrew publication**:
   - Check Homebrew tap repository for updated formula
   - Formula should reference new version and checksums

6. **Test installation**:
   ```bash
   brew tap sirkirby/ten-second-tom
   brew install ten-second-tom
   ten-second-tom --version
   # Should show v0.1.0-test
   ```

### Expected Results

✅ **Validate Version job**:
- Confirms `v0.1.0-test` follows semver
- Checks version doesn't already exist

✅ **Build Release jobs** (~10-15 minutes):
- Produce three platform executables
- Each tagged with version metadata

✅ **Create Release job**:
- GitHub Release created at `/releases/tag/v0.1.0-test`
- Release has three binary attachments
- Release notes auto-generated from commits

✅ **Publish Homebrew job** (after approval):
- Formula updated in tap repository
- New commit in tap shows version bump
- Formula contains correct SHA256 checksums

✅ **Installation works**:
- `brew install` succeeds
- Installed binary runs and shows correct version

---

## Scenario 4: Coverage Comment Trigger

**Goal**: Verify that significant coverage changes post PR comments

### Steps

1. **Create branch with coverage increase**:
   ```bash
   git checkout -b test/coverage-increase
   ```

2. **Add tests for previously uncovered code**:
   - Identify file with <80% coverage
   - Write comprehensive tests
   - Commit and push

3. **Create PR**

4. **Wait for coverage job to complete**

5. **Check PR comments**

### Expected Results

✅ **Coverage increases by ≥5%**:
- PR comment posted automatically
- Comment shows:
  - Previous coverage percentage
  - New coverage percentage
  - Difference (e.g., +6.5%)
  - Link to full coverage report
  - Top 5 files with largest coverage changes

Example comment:
```markdown
## 📊 Coverage Report

**Current**: 85.0% | **Previous**: 78.5% | **Change**: +6.5% ✅

[View Full Report](link to artifact)

### Top Changes
- `src/Features/Auth/Handler.cs`: 45% → 98% (+53%)
- `src/Features/Retry/Policy.cs`: 60% → 100% (+40%)
- ...
```

---

## Troubleshooting Guide

### Workflow Not Triggering

**Symptom**: PR created but no workflow run appears

**Checks**:
- Verify `.github/workflows/pr-validation.yml` exists on target branch (main)
- Check workflow `on:` trigger includes `pull_request`
- Ensure repository has Actions enabled (Settings → Actions → "Allow all actions")

### Workflow Fails with "Restore Failed"

**Symptom**: Build job fails during `dotnet restore`

**Solution**:
- Verify `packages.lock.json` exists and is committed
- Check NuGet package sources are accessible
- Clear cache by updating cache key

### Coverage Job Fails with "Threshold Not Met"

**Symptom**: Coverage job shows "Coverage 75% (required 80%)"

**Solution**: This is expected behavior if coverage is actually below 80%
- Add more tests to increase coverage
- Verify coverage calculation is correct
- Check for excluded files that shouldn't be excluded

### Build Artifacts Are Too Large

**Symptom**: Executable >50MB, violating NFR-018

**Solution**:
- Verify `PublishTrimmed=true` in publish command
- Check for accidentally included debug symbols
- Consider excluding unnecessary culture resources

---

## Performance Benchmarks

Expected timing for typical PRs:

| Stage | Target | Typical Actual |
|-------|--------|----------------|
| Checkout & Setup | <1 min | ~30s |
| Build | ≤2 min | ~1m 30s |
| Test | ≤5 min | ~3m 45s |
| Coverage | ≤3 min | ~2m 15s |
| **Total PR Validation** | **≤10 min** | **~8 min** |
| Main Branch Build | ≤15 min | ~12 min |
| Release (full) | ≤30 min | ~25 min |

If actual times significantly exceed targets:
1. Check for caching issues (packages not cached)
2. Consider parallelizing unit/integration tests
3. Review test execution time for slow tests

---

## Cleanup

After validation testing:

1. **Delete test branches**:
   ```bash
   git push origin --delete test/pr-validation
   git push origin --delete test/coverage-increase
   ```

2. **Delete test tags**:
   ```bash
   git push origin --delete v0.1.0-test
   git tag -d v0.1.0-test
   ```

3. **Delete test GitHub releases**:
   - Navigate to repository Releases
   - Delete test release `v0.1.0-test`

4. **Uninstall test Homebrew formula** (if installed):
   ```bash
   brew uninstall ten-second-tom
   brew untap sirkirby/ten-second-tom
   ```

---

## Success Criteria

All scenarios pass when:

✅ PR validation workflow runs on every PR and enforces quality gates  
✅ Build workflow produces three platform-specific executables  
✅ Release workflow creates GitHub release and publishes to Homebrew  
✅ Coverage comments post when coverage changes by ≥5%  
✅ All workflows complete within performance targets  
✅ Artifacts are downloadable and runnable  

---

## Next Steps After Validation

Once quickstart scenarios pass:

1. Configure branch protection rules requiring "PR Validation" check
2. Set up production secrets for Winget and Chocolatey (Phase 2)
3. Create actual release (not test tag) when ready
4. Monitor workflow performance and optimize if needed
5. Document workflow usage in main README

**Status**: Quickstart complete, ready for Phase 4 implementation and Phase 5 validation.
