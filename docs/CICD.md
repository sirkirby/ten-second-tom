# CI/CD Documentation

**Last Updated**: 2025-10-03  
**Status**: Active

## Overview

This document describes the GitHub Actions CI/CD pipeline for Ten Second Tom. The pipeline consists of three workflows that automate testing, building, and distributing the application across multiple platforms.

---

## Required GitHub Secrets

The following secrets must be configured in the repository settings (`Settings → Secrets and variables → Actions`):

### For Homebrew Publication (Release Workflow)

**`HOMEBREW_TAP_TOKEN`**
- **Purpose**: Personal access token for pushing formula updates to Homebrew tap repository
- **Scope**: `repo` (Full control of private repositories)
- **Setup**:
  1. Go to GitHub Settings → Developer settings → Personal access tokens → Fine-grained tokens
  2. Click "Generate new token"
  3. Set name: "Homebrew Tap Token for Ten Second Tom"
  4. Set expiration: 1 year (or custom)
  5. Select repository access: Only select repositories → `sirkirby/homebrew-ten-second-tom` (your tap)
  6. Set repository permissions:
     - Contents: Read and write
     - Metadata: Read-only
  7. Generate token and copy immediately (won't be shown again)
  8. Add to repository secrets as `HOMEBREW_TAP_TOKEN`

### For Future Package Managers (Phase 2)

**`WINGET_TOKEN`** (Not yet implemented)
- **Purpose**: Token for creating pull requests to microsoft/winget-pkgs repository
- **Status**: Manual process documented in release workflow; automation planned for Phase 2

**`CHOCOLATEY_API_KEY`** (Not yet implemented)
- **Purpose**: API key for publishing packages to chocolatey.org
- **Status**: Manual process documented in release workflow; automation planned for Phase 2

---

## Workflows

### 1. PR Validation Workflow

**File**: `.github/workflows/pr-validation.yml`  
**Trigger**: Pull requests targeting `main` branch  
**Purpose**: Validate code quality before merging

**Jobs**:
- **Build**: Compile code with zero warnings
- **Test**: Run all unit and integration tests
- **Coverage**: Prevent coverage regression, track line coverage
- **Validate**: Aggregate status from all jobs

**Coverage Strategy**:
- **Primary Metric**: Line coverage (% of executable lines hit by tests)
- **Regression Prevention**: PRs cannot decrease coverage by more than 0.5 percentage points
- **No Absolute Threshold**: Focus on preventing regression, not arbitrary targets
- **Other Metrics Tracked**: Branch coverage, method coverage (in reports, not enforced)

**Why Line Coverage?**
- Most widely understood and standardized metric
- Easier to reason about than branch or method coverage
- Industry standard for baseline quality gates
- Branch/method coverage available in detailed reports for deeper analysis

**Performance Targets**:
- Build job: ≤2 minutes
- Test job: ≤5 minutes
- Coverage job: ≤3 minutes
- Total: ≤10 minutes

**Artifacts**:
- Test results (TRX format, 7 days retention)
- Coverage reports (HTML/XML with line/branch/method metrics, 30 days retention)

**Coverage Diff Comments**: Automatically posts PR comment when coverage changes by ≥1 percentage point

---

### 2. Build Workflow

**File**: `.github/workflows/build.yml`  
**Trigger**: Pushes to `main` branch  
**Purpose**: Build cross-platform self-contained executables

**Jobs**:
- **Test**: Re-run all tests on main branch
- **Build macOS x64**: Build for Intel Macs
- **Build macOS ARM64**: Build for Apple Silicon Macs
- **Build Windows x64**: Build for Windows
- **Smoke Test**: Verify all executables run successfully

**Performance Targets**:
- Test job: ≤5 minutes
- Each build job: ≤5 minutes (parallel)
- Smoke tests: <1 minute each
- Total: ≤15 minutes

**Artifacts**:
- Self-contained executables (<50MB each, 90 days retention)
- SHA256 checksums for verification
- Build metadata (version, commit, timestamp, size)

**Platforms**:
- macOS x64 (`osx-x64`)
- macOS ARM64 (`osx-arm64`)
- Windows x64 (`win-x64`)

---

### 3. Release Workflow

**File**: `.github/workflows/release.yml`  
**Trigger**: Semantic version tags (e.g., `v1.0.0`)  
**Purpose**: Automated distribution to package managers

**Jobs**:
1. **Version Validation**: Verify semantic version format and uniqueness
2. **Build Release Artifacts**: Build all platform executables at tagged version
3. **GitHub Release**: Create release with binaries and checksums
4. **Homebrew Publication** (requires approval): Update Homebrew tap formula
5. **Documentation Generation**: Create Winget and Chocolatey manifest templates

**Approval Gate**: Homebrew publication requires approval from `@sirkirby` (configured in CODEOWNERS)

**Performance Targets**:
- Version validation: <1 minute
- Build artifacts: ≤15 minutes
- GitHub release: ≤2 minutes
- Homebrew publication: ≤5 minutes
- Total (excluding approval): ≤25 minutes

**Package Managers**:
- ✅ Homebrew (automated)
- 📋 Winget (manual via generated manifest)
- 📋 Chocolatey (manual via generated manifest)

---

## Homebrew Tap Setup

To enable automated Homebrew publication, create a tap repository:

### 1. Create Tap Repository

```bash
# Create a new repository on GitHub named: homebrew-ten-second-tom
# Repository must be public for Homebrew
# Initialize with README
```

### 2. Create Initial Formula

Create `Formula/ten-second-tom.rb`:

```ruby
class TenSecondTom < Formula
  desc "CLI tool for managing daily tasks"
  homepage "https://github.com/sirkirby/ten-second-tom"
  url "https://github.com/sirkirby/ten-second-tom/releases/download/v1.0.0/ten-second-tom-osx-x64"
  sha256 "PLACEHOLDER_CHECKSUM"
  version "1.0.0"

  def install
    bin.install "ten-second-tom-osx-x64" => "ten-second-tom"
  end

  test do
    system "#{bin}/ten-second-tom", "--version"
  end
end
```

### 3. Configure Token

Add `HOMEBREW_TAP_TOKEN` to repository secrets (see Required GitHub Secrets above).

### 4. Test Installation Locally

```bash
# Add tap
brew tap sirkirby/ten-second-tom

# Install from tap
brew install sirkirby/ten-second-tom/ten-second-tom

# Verify installation
ten-second-tom --version

# Uninstall
brew uninstall ten-second-tom
brew untap sirkirby/ten-second-tom
```

---

## Workflow Badges

Add these badges to `README.md` to show workflow status:

```markdown
[![PR Validation](https://github.com/sirkirby/ten-second-tom/actions/workflows/pr-validation.yml/badge.svg)](https://github.com/sirkirby/ten-second-tom/actions/workflows/pr-validation.yml)
[![Build](https://github.com/sirkirby/ten-second-tom/actions/workflows/build.yml/badge.svg)](https://github.com/sirkirby/ten-second-tom/actions/workflows/build.yml)
[![Release](https://github.com/sirkirby/ten-second-tom/actions/workflows/release.yml/badge.svg)](https://github.com/sirkirby/ten-second-tom/actions/workflows/release.yml)
```

---

## Troubleshooting

### Build Job Failures

**Problem**: Compiler warnings causing build failure

**Solution**:
1. Check job logs for warning messages
2. Fix warnings in source code
3. Run `dotnet build --no-incremental --warnaserror` locally to reproduce
4. Push fix to branch

**Problem**: NuGet restore failures

**Solution**:
1. Check network connectivity in job logs
2. Verify package sources in `NuGet.config`
3. Clear cache by triggering workflow with `[clear cache]` in commit message
4. Check for package deprecation warnings

---

### Test Job Failures

**Problem**: Tests passing locally but failing in CI

**Solution**:
1. Check for timing-dependent tests
2. Check for file system path dependencies
3. Check for environment variable dependencies
4. Run tests in Release configuration locally: `dotnet test -c Release`
5. Review test logs in artifact download

**Problem**: Test timeout

**Solution**:
1. Identify slow tests in logs
2. Optimize test performance
3. Increase timeout in workflow if justified (current: 5 minutes)

---

### Coverage Job Failures

**Problem**: Coverage below 80% threshold

**Solution**:
1. Download coverage report artifact from job
2. Open `index.html` to see uncovered code
3. Add tests for uncovered lines
4. Target: ≥80% line coverage

**Problem**: Coverage diff showing incorrect change

**Solution**:
1. Check if baseline coverage was cached correctly
2. Verify target branch (main) has recent coverage data
3. Re-run workflow to refresh cache

---

### Build Workflow Issues

**Problem**: Executable size exceeds 50MB

**Solution**:
1. Check assembly trimming settings in `.csproj`
2. Enable more aggressive trimming:
   ```xml
   <TrimMode>link</TrimMode>
   <InvariantGlobalization>true</InvariantGlobalization>
   ```
3. Review included dependencies for unnecessary packages
4. Consider IL linking for further size reduction

**Problem**: Smoke test failures

**Solution**:
1. Download artifact to test locally
2. Check executable permissions (macOS/Linux)
3. Verify runtime dependencies are included
4. Test `--version` command manually

---

### Release Workflow Issues

**Problem**: Version validation failure

**Solution**:
1. Verify tag format: `v1.0.0` (must start with 'v')
2. Check semantic version format: MAJOR.MINOR.PATCH
3. Verify version doesn't already exist in GitHub releases
4. Delete and recreate tag if needed:
   ```bash
   git tag -d v1.0.0
   git push origin :refs/tags/v1.0.0
   git tag v1.0.0
   git push origin v1.0.0
   ```

**Problem**: Homebrew publication failure

**Solution**:
1. Verify `HOMEBREW_TAP_TOKEN` is configured correctly
2. Check token has `repo` scope on tap repository
3. Verify tap repository exists and is public
4. Check formula syntax in tap repository
5. Test formula update locally:
   ```bash
   brew edit sirkirby/ten-second-tom/ten-second-tom
   brew audit --strict sirkirby/ten-second-tom/ten-second-tom
   ```

**Problem**: Approval not requested

**Solution**:
1. Verify `.github/CODEOWNERS` is configured
2. Verify GitHub Environment "production" exists
3. Check required reviewers are configured in Environment settings
4. Verify tag trigger matches environment deployment branches

---

## Performance Optimization Tips

### Reduce Build Time

1. **Use caching**:
   - NuGet packages cached by default
   - Consider caching build outputs for incremental builds

2. **Parallelize jobs**:
   - Build jobs run in parallel (already implemented)
   - Consider splitting test suite if needed

3. **Optimize dependencies**:
   - Remove unused packages
   - Use faster alternative packages where possible

### Reduce Test Time

1. **Profile slow tests**:
   - Use test logging to identify slow tests
   - Optimize or refactor slow tests

2. **Use test filtering**:
   - Consider separating fast unit tests from slow integration tests
   - Run integration tests only on main branch if needed

3. **Parallelize test execution**:
   - xUnit parallelizes by default
   - Ensure tests are thread-safe

---

## Contact & Support

For issues with CI/CD workflows:

1. Check this troubleshooting guide first
2. Review workflow logs in Actions tab
3. Check existing Issues for similar problems
4. Create new Issue with:
   - Workflow name and run number
   - Error messages from logs
   - Steps to reproduce (if applicable)
   - Relevant workflow file snippets

**Maintainer**: @sirkirby  
**Documentation**: This file (`docs/CICD.md`)  
**Workflow Files**: `.github/workflows/`

---

## Future Enhancements

### Code Signing (Phase 2)

**macOS**:
- Apple Developer Program membership available
- Implement code signing via `codesign` command
- Implement notarization via `notarytool`
- Benefits: Eliminates Gatekeeper warnings entirely

**Windows**:
- Code signing certificate required ($100-400/year)
- Implement signing via `signtool` command
- Benefits: Improves SmartScreen reputation

**Status**: Deferred per research.md; unsigned binaries work for initial release

### Winget Automation (Phase 2)

- Automate manifest generation (currently manual)
- Automate PR creation to microsoft/winget-pkgs
- Configure `WINGET_TOKEN` for authentication

### Chocolatey Automation (Phase 2)

- Automate package creation and publication
- Configure `CHOCOLATEY_API_KEY`
- Test package installation on Windows

---

**Document Version**: 1.0.0  
**Last Updated**: 2025-10-03
