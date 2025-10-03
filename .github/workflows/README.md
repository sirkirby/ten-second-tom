# GitHub Actions Workflows

This directory contains GitHub Actions workflows for the Ten Second Tom project's CI/CD pipeline.

## Workflows

### 1. PR Validation (`pr-validation.yml`)

**Trigger**: Pull requests to `main` branch  
**Purpose**: Validate code quality before merging  
**Status**: [![PR Validation](https://github.com/sirkirby/ten-second-tom/actions/workflows/pr-validation.yml/badge.svg)](https://github.com/sirkirby/ten-second-tom/actions/workflows/pr-validation.yml)

**Jobs**:
- **Build**: Compile code with zero warnings (≤2 minutes)
- **Test**: Run all unit and integration tests (≤5 minutes)
- **Coverage**: Enforce 80% minimum code coverage (≤3 minutes)
- **Validate**: Aggregate status from all jobs

**Performance Target**: ≤10 minutes total

**Required secrets**: None (uses repository token)

**Artifacts**:
- Test results (TRX format, 7 days retention)
- Coverage reports (HTML/XML, 30 days retention)

**Features**:
- Automatic PR comments when coverage changes by ≥5%
- Cancels in-progress runs when new commits pushed
- Caches NuGet packages for faster execution
- Treats compiler warnings as errors

---

### 2. Build Workflow (`build.yml`)

**Trigger**: Pushes to `main` branch  
**Purpose**: Build cross-platform executables  
**Status**: *Coming in Phase 2*

**Jobs**:
- **Test**: Re-run all tests on main branch
- **Build macOS x64**: Self-contained executable for Intel Macs
- **Build macOS ARM64**: Self-contained executable for Apple Silicon
- **Build Windows x64**: Self-contained executable for Windows
- **Smoke Test**: Verify all executables run successfully

**Performance Target**: ≤15 minutes total

**Required secrets**: None

**Artifacts**:
- Self-contained executables (<50MB each, 90 days retention)
- SHA256 checksums
- Build metadata (version, commit, timestamp, size)

---

### 3. Release Workflow (`release.yml`)

**Trigger**: Semantic version tags (e.g., `v1.0.0`)  
**Purpose**: Automated distribution to package managers  
**Status**: *Coming in Phase 3*

**Jobs**:
1. **Version Validation**: Verify semantic version format
2. **Build Release Artifacts**: Build all platform executables
3. **GitHub Release**: Create release with binaries and checksums
4. **Homebrew Publication**: Update Homebrew tap (requires approval)

**Performance Target**: ≤30 minutes (excluding approval)

**Required secrets**:
- `HOMEBREW_TAP_TOKEN`: Personal access token for Homebrew tap repository

**Features**:
- CODEOWNERS-based approval gate for Homebrew publication
- Automated release notes generation
- Package manager manifest templates for Winget and Chocolatey

---

## Common Failure Modes

### Build Job Failures

**Issue**: Compiler warnings causing failure

**Solution**:
1. Check job logs for warning messages
2. Fix warnings in source code
3. Run `dotnet build --no-incremental --warnaserror` locally

---

### Test Job Failures

**Issue**: Tests passing locally but failing in CI

**Solution**:
1. Check for environment-specific dependencies
2. Check for timing-dependent tests
3. Run `dotnet test -c Release` locally to reproduce

---

### Coverage Job Failures

**Issue**: Coverage below 80% threshold

**Solution**:
1. Download coverage report artifact
2. Open `index.html` to identify uncovered code
3. Add tests for uncovered lines
4. Verify coverage meets 80% threshold

---

## Required Secrets

### For PR Validation (Current)

No secrets required - uses default `GITHUB_TOKEN`

### For Homebrew Publication (Phase 3)

**`HOMEBREW_TAP_TOKEN`**:
- Personal access token with `repo` scope
- Must have write access to Homebrew tap repository
- See [`docs/CICD.md`](../../docs/CICD.md) for setup instructions

---

## Performance Monitoring

All workflows log execution times. Monitor performance against these targets:

| Workflow | Target | Current |
|----------|--------|---------|
| PR Validation | ≤10 minutes | Monitor via Actions tab |
| Build | ≤15 minutes | *Not yet implemented* |
| Release | ≤30 minutes | *Not yet implemented* |

If any job consistently exceeds its target:
1. Review job logs for bottlenecks
2. Consider parallelization opportunities
3. Optimize test suite or build configuration
4. Increase runner resources if necessary

---

## Troubleshooting

For detailed troubleshooting guides, see [`docs/CICD.md`](../../docs/CICD.md).

Common issues:
- **Cache issues**: Add `[clear cache]` to commit message
- **Artifact issues**: Check retention policy and storage limits
- **Permission issues**: Verify repository token permissions in Settings → Actions

---

## Local Testing

Test workflows locally using [act](https://github.com/nektos/act):

```bash
# Install act (macOS)
brew install act

# Test PR validation workflow
act pull_request -W .github/workflows/pr-validation.yml

# Test specific job
act -j build -W .github/workflows/pr-validation.yml
```

**Note**: Some features (caching, artifacts) may not work identically in local testing.

---

## Contributing

When adding or modifying workflows:

1. **Test locally** with `act` before pushing
2. **Update documentation** in this README and `docs/CICD.md`
3. **Follow naming conventions**: Use descriptive job and step names
4. **Add comments**: Explain non-obvious configuration
5. **Monitor performance**: Ensure jobs meet target times
6. **Request review**: All workflow changes require maintainer approval (see `.github/CODEOWNERS`)

---

## Documentation

- **Workflow Details**: This file
- **Setup Instructions**: [`docs/CICD.md`](../../docs/CICD.md)
- **Troubleshooting**: [`docs/CICD.md`](../../docs/CICD.md#troubleshooting)
- **GitHub Actions Docs**: https://docs.github.com/en/actions

---

**Last Updated**: 2025-10-03  
**Maintainer**: @sirkirby
