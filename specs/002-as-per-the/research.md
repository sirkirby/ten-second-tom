# Research: GitHub Actions CI/CD Pipeline

**Feature**: GitHub Actions CI/CD Pipeline  
**Date**: 2025-10-03  
**Status**: Complete

## Overview

This document consolidates research findings for implementing a comprehensive GitHub Actions CI/CD pipeline for the Ten Second Tom CLI application. Research focused on three critical areas identified in the technical context: code signing requirements, package manager publication processes, and CI/CD best practices for .NET 9 applications.

---

## Research Area 1: Code Signing Requirements

### Decision: Conditional Code Signing Based on Package Manager Requirements

**Rationale**: Different package managers have varying code signing requirements. After researching each target platform, we've determined specific signing needs.

### Homebrew (macOS)

**Finding**: Homebrew does **NOT** require code signing for CLI applications distributed as binaries.

- Homebrew formulas can reference unsigned binaries
- Code signing is optional and primarily beneficial for notarization (which prevents Gatekeeper warnings)
- For CLI tools installed via Homebrew, users already trust the package manager, reducing the need for Apple Developer Program enrollment

**Decision**: **Defer macOS code signing** to a future enhancement. Homebrew distribution can proceed without signing.

**Context**: The project maintainer is a member of the Apple Developer Program, making code signing and notarization available for future implementation. This will be valuable for providing a fully notarized experience that eliminates Gatekeeper warnings entirely.

**Alternative Considered**: Immediate implementation of code signing and notarization. Deferred because:
- Not required for Homebrew distribution (initial release priority)
- Adds workflow complexity (secure certificate management, notarization API integration)
- CLI tools have lower security scrutiny than GUI applications
- Can be incrementally added in Phase 2 once distribution is established
- Users can bypass Gatekeeper warnings if needed for unsigned builds

**Future Enhancement Path** (when prioritized):
1. Export Developer ID Application certificate from Apple Developer account
2. Store certificate and password as GitHub Secrets
3. Add signing step to macOS build job using `codesign` command
4. Submit signed binary to Apple notarization service via `notarytool`
5. Staple notarization ticket to binary using `stapler` command
6. Update Homebrew formula to reference notarized binary

### Winget (Windows)

**Finding**: Windows Package Manager (winget) does **NOT** require code signing for inclusion in the community repository.

- Winget manifests can reference unsigned executables
- Code signing is optional and primarily beneficial for SmartScreen reputation
- Community-maintained packages regularly include unsigned binaries

**Decision**: **Defer Windows code signing** to a future enhancement. Winget distribution can proceed without signing.

**Alternative Considered**: Code signing certificate purchase ($100-400/year). Rejected because:
- Not required for winget distribution
- SmartScreen reputation builds over time regardless of signing
- Adds infrastructure complexity (secure key storage, signing process)
- Can be added later when/if needed

### Chocolatey (Windows)

**Finding**: Chocolatey has the same requirements as Winget regarding code signing.

- Community packages can contain unsigned binaries
- Moderation process focuses on package quality, not binary signing
- Code signing is optional but not enforced

**Decision**: **Defer Windows code signing** for Chocolatey as well.

### Implementation Impact

**For Phase 1**: Code signing is **deferred** to a future enhancement. While Apple Developer Program membership is available, initial implementation focuses on establishing distribution channels without the added complexity of certificate management and notarization workflows.

**For Workflows**:
- Build workflows will produce unsigned binaries
- Release workflows will publish unsigned binaries
- README will document that binaries are unsigned and explain verification via checksums
- GitHub releases will include SHA256 checksums for manual verification

**For Future Enhancement** (recommended Phase 2 or 3):
- Apple Developer Program membership enables macOS code signing and notarization
- Windows code signing certificate can be obtained ($100-400/year) if user feedback indicates need
- Signing infrastructure can be added incrementally (macOS first via existing Developer Program access, then Windows if justified)
- Notarized macOS binaries will eliminate all Gatekeeper warnings
- Signed Windows binaries will improve SmartScreen reputation over time

---

## Research Area 2: Package Manager Publication

### Decision: Use Official Package Manager CLIs and GitHub Actions

**Rationale**: Each package manager has established automation patterns with GitHub Actions marketplace actions available.

### Homebrew Publication

**Process**:
1. Create Homebrew tap repository (e.g., `sirkirby/homebrew-ten-second-tom`)
2. Generate formula file with binary URL, SHA256 checksum
3. Push formula to tap repository on release
4. Users install via `brew install sirkirby/ten-second-tom/ten-second-tom`

**GitHub Actions Integration**:
- Use `dawidd6/action-homebrew-bump-formula@v3` action
- Requires GitHub token with repo write access
- Automatically calculates checksums and updates formula

**Authentication**: GitHub token (GITHUB_TOKEN automatic, or PAT for cross-repo access)

**Best Practices**:
- Keep formula in separate tap repository for cleaner organization
- Use template formula file checked into source control
- Automate version bumps on tag push
- Include both macOS x64 and ARM64 binaries in formula

### Winget Publication

**Process**:
1. Generate winget manifest (YAML) with package metadata
2. Fork microsoft/winget-pkgs repository
3. Submit pull request with new/updated manifest
4. Wait for automated validation and community review
5. Package becomes available after PR merge

**GitHub Actions Integration**:
- Use `vedantmgoyal2009/winget-releaser@v2` action
- Automates manifest generation and PR submission
- Requires GitHub PAT token with public_repo scope

**Authentication**: GitHub PAT (personal access token) stored as secret

**Best Practices**:
- Use semantic versioning strictly (required by winget)
- Include detailed package metadata (description, license, tags)
- Provide installer URL and SHA256 checksum
- First submission requires manual review; updates are faster

**Timing**: Initial PR review can take 1-3 days. Automate subsequent updates.

### Chocolatey Publication

**Process**:
1. Generate nuspec file with package metadata
2. Create Chocolatey package (.nupkg)
3. Publish to chocolatey.org via API
4. Package undergoes automated and manual moderation
5. Package becomes available after moderation approval

**GitHub Actions Integration**:
- Use `chocolatey/setup-chocolatey@v2` and `choco push` command
- Requires Chocolatey API key stored as secret

**Authentication**: Chocolatey API key (obtained from chocolatey.org account)

**Best Practices**:
- Use semantic versioning
- Include verification file (checksums)
- Provide installation and uninstallation scripts
- First submission requires manual moderation; trusted packages have faster updates

**Timing**: Initial moderation can take 1-3 days. Build trusted package status through consistent quality.

### Alternatives Considered

**Alternative 1**: Manual publication process
- **Rejected**: Violates constitution principle V (automated releases)

**Alternative 2**: Custom publication scripts
- **Rejected**: Reinventing wheel; official actions are well-maintained and tested

**Alternative 3**: Publish only to GitHub Releases, skip package managers
- **Rejected**: Violates constitution principle VI (cross-platform distribution via package managers)

---

## Research Area 3: .NET 9 CI/CD Best Practices

### Decision: Use dotnet CLI with coverlet and ReportGenerator

**Rationale**: Microsoft's official tooling provides excellent GitHub Actions integration and comprehensive coverage reporting.

### Test Execution

**Tool**: `dotnet test` with xUnit

**Configuration**:
```yaml
- name: Run Tests
  run: dotnet test --configuration Release --no-build --verbosity normal
```

**Best Practices**:
- Run tests after build (use `--no-build` to avoid double compilation)
- Use `--verbosity normal` for CI (detailed enough, not overwhelming)
- Separate unit and integration tests if execution time becomes issue
- Use `--logger "trx"` for test results artifacts

### Coverage Calculation

**Tool**: Coverlet + ReportGenerator

**Configuration**:
```yaml
- name: Run Tests with Coverage
  run: dotnet test --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory ./coverage

- name: Generate Coverage Report
  uses: danielpalme/ReportGenerator-GitHub-Action@5
  with:
    reports: './coverage/**/coverage.cobertura.xml'
    targetdir: './coverage-report'
    reporttypes: 'HtmlInline;Cobertura;MarkdownSummary'
```

**Coverage Enforcement**:
```yaml
- name: Check Coverage Threshold
  run: |
    coverage=$(grep -oP 'line-rate="\K[0-9.]+' coverage-report/Cobertura.xml | head -1)
    threshold=0.80
    if (( $(echo "$coverage < $threshold" | bc -l) )); then
      echo "Coverage $coverage is below threshold $threshold"
      exit 1
    fi
```

**Best Practices**:
- Use Cobertura format for compatibility
- Generate HTML report for human review
- Use MarkdownSummary for PR comments
- Cache coverage results between runs for diff calculation

**Alternatives Considered**:
- **Codecov/Coveralls**: Rejected due to third-party dependency and cost
- **Fine Code Coverage**: Rejected; designed for local IDE use, not CI

### Cross-Platform Builds

**Strategy**: Matrix build with GitHub-hosted runners

**Configuration**:
```yaml
strategy:
  matrix:
    os: [ubuntu-latest, macos-latest, windows-latest]
    include:
      - os: ubuntu-latest
        rid: linux-x64
      - os: macos-latest
        rid: osx-x64
      - os: macos-latest
        rid: osx-arm64
      - os: windows-latest
        rid: win-x64
```

**Build Commands**:
```yaml
- name: Publish Self-Contained
  run: dotnet publish src/TenSecondTom.csproj -c Release -r ${{ matrix.rid }} --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true
```

**Best Practices**:
- Use `-p:PublishSingleFile=true` for single executable
- Use `-p:PublishTrimmed=true` to reduce size (target <50MB per spec NFR-018)
- Test published executable with smoke test
- Store artifacts with version metadata

**Size Optimization**:
- Enable trimming and single-file publish
- Exclude unnecessary culture resources
- Use `PublishReadyToRun=false` if size is critical (slight startup cost)

---

## Research Area 4: GitHub Actions Workflow Organization

### Decision: Three Workflow Files with Reusable Components

**Rationale**: Separation of concerns, clear triggers, maintainable structure.

### Workflow 1: PR Validation (`pr-validation.yml`)

**Trigger**: `pull_request` on all branches targeting `main`

**Jobs**:
1. **Build**: Compile code, check for warnings
2. **Test**: Run unit and integration tests
3. **Coverage**: Calculate coverage, check 80% threshold, post PR comment if >5% change
4. **Validate**: Summary job that fails if any check fails

**Branch Protection**: Configure as required status check

**Performance Target**: <10 minutes total (spec NFR-001, FR-029)

### Workflow 2: Build (`build.yml`)

**Trigger**: `push` to `main` branch (after PR merge)

**Jobs**:
1. **Test**: Re-run all tests on main
2. **Build-Matrix**: Build self-contained executables for all platforms (matrix)
3. **Verify**: Smoke test each executable
4. **Upload**: Store artifacts with version metadata

**Artifacts**: Executables stored for 90 days, available for manual testing

**Performance Target**: <15 minutes total (spec NFR-003)

### Workflow 3: Release (`release.yml`)

**Trigger**: `push` of tags matching `v*.*.*` pattern

**Jobs**:
1. **Validate-Version**: Check semantic version format
2. **Build-Release**: Build all platform executables with release configuration
3. **Create-Release**: Create GitHub release with release notes and binaries
4. **Publish-Homebrew**: Update Homebrew tap (auto-approve via CODEOWNERS)
5. **Publish-Winget**: Submit winget manifest PR (requires approval)
6. **Publish-Chocolatey**: Push Chocolatey package (requires approval)

**Approval Gate**: Winget and Chocolatey jobs use `environment: production` with CODEOWNERS approval

**Performance Target**: <30 minutes total (spec NFR-004)

### Reusable Components

**Composite Actions** (optional, for DRY):
- `.github/actions/setup-dotnet/action.yml`: Setup .NET 9 SDK with caching
- `.github/actions/run-tests/action.yml`: Test execution with standardized reporting

**Workflow Call** (for shared logic):
- Consider if multiple workflows need identical test/build steps

**Best Practices**:
- Use `actions/checkout@v4` (latest)
- Use `actions/setup-dotnet@v4` with caching
- Use `actions/upload-artifact@v4` for build outputs
- Pin action versions for security and reproducibility
- Use `concurrency` groups to cancel outdated workflow runs

---

## Research Area 5: Secrets and Security

### Decision: GitHub Secrets with Environment Protection Rules

**Rationale**: GitHub native secrets management with approval gates for production.

### Required Secrets

1. **GITHUB_TOKEN**: Automatically provided, used for:
   - Homebrew tap updates (if same organization)
   - Creating releases
   - Posting PR comments

2. **HOMEBREW_TAP_TOKEN**: GitHub PAT (if tap is in different org)
   - Scope: `public_repo` or `repo`
   - Used for pushing to Homebrew tap repository

3. **WINGET_TOKEN**: GitHub PAT for winget-pkgs PR automation
   - Scope: `public_repo`
   - Used by winget-releaser action

4. **CHOCOLATEY_API_KEY**: Chocolatey API key
   - Obtained from chocolatey.org account settings
   - Used for `choco push` command

### Environment Protection

**Environment**: `production`
- Required reviewers: Members in CODEOWNERS file
- Deployment branches: Tags matching `v*`
- Secrets: WINGET_TOKEN, CHOCOLATEY_API_KEY

**Configuration**:
```yaml
jobs:
  publish-winget:
    runs-on: ubuntu-latest
    environment: production
    steps:
      - name: Publish to Winget
        uses: vedantmgoyal2009/winget-releaser@v2
        with:
          token: ${{ secrets.WINGET_TOKEN }}
```

### CODEOWNERS File

**Location**: `.github/CODEOWNERS`

**Content**:
```
# Release approval required from repository admins
/.github/workflows/release.yml @sirkirby
```

**Effect**: Release workflow requires approval from @sirkirby (or specified team)

### Best Practices

- Rotate secrets annually
- Use environment-specific secrets (dev/prod) if needed
- Never log secret values
- Use least-privilege scopes on PATs
- Document secret requirements in README

---

## Research Area 6: Caching and Performance Optimization

### Decision: Multi-Layer Caching Strategy

**Rationale**: Maximize performance to meet timing requirements (FR-029, NFR-001-004).

### NuGet Package Caching

**Strategy**: Use `actions/setup-dotnet` built-in caching

**Configuration**:
```yaml
- name: Setup .NET
  uses: actions/setup-dotnet@v4
  with:
    dotnet-version: '9.0.x'
    cache: true
    cache-dependency-path: '**/packages.lock.json'
```

**Impact**: Reduces package restore time from ~30s to ~5s

**Requirement**: Ensure `packages.lock.json` committed to repository

### Build Output Caching

**Strategy**: Cache `obj/` and `bin/` directories between steps

**Configuration**:
```yaml
- name: Cache Build
  uses: actions/cache@v4
  with:
    path: |
      **/obj
      **/bin
    key: build-${{ runner.os }}-${{ hashFiles('**/*.csproj') }}-${{ github.sha }}
    restore-keys: |
      build-${{ runner.os }}-${{ hashFiles('**/*.csproj') }}-
      build-${{ runner.os }}-
```

**Impact**: Reduces incremental build time

### Coverage Report Caching

**Strategy**: Store previous coverage for diff calculation

**Configuration**:
```yaml
- name: Cache Coverage
  uses: actions/cache@v4
  with:
    path: ./coverage-baseline
    key: coverage-${{ github.base_ref }}-${{ github.event.pull_request.base.sha }}
```

**Impact**: Enables accurate coverage diff on PRs

### Alternatives Considered

**Alternative 1**: No caching
- **Rejected**: Violates performance requirements

**Alternative 2**: Self-hosted runners with persistent cache
- **Rejected**: Adds infrastructure complexity, violates simplicity principle

**Alternative 3**: ccache for C++ dependencies
- **Rejected**: Not applicable to .NET projects

---

## Implementation Priorities

### Phase 1 (MVP - This Release)

1. ✅ PR validation workflow (FR-001 through FR-012, FR-026 through FR-030)
2. ✅ Build workflow (FR-013 through FR-018)
3. ✅ Release workflow skeleton (FR-022 through FR-025)
4. ✅ Homebrew publication (FR-019)
5. ✅ GitHub Release creation (FR-024)
6. ✅ CODEOWNERS approval gate (FR-025, NFR-010)

### Phase 2 (Follow-up)

1. Winget publication (FR-020) - requires microsoft/winget-pkgs fork and initial manual submission
2. Chocolatey publication (FR-021) - requires chocolatey.org account and API key
3. Build success tracking (FR-032)
4. Enhanced notification system (FR-031, FR-033)

### Phase 3 (Future Enhancements)

1. Code signing infrastructure (NFR-009)
2. Performance benchmarking integration
3. Security scanning (SARIF, dependency scanning)
4. Canary/beta release channels

---

## Risks and Mitigations

| Risk | Impact | Likelihood | Mitigation |
|------|--------|-----------|------------|
| Package manager moderation delays | Users can't install immediately | Medium | Document manual GitHub Release installation, provide direct download links |
| Build time exceeds 10min (FR-029) | Slow developer feedback | Low | Implement aggressive caching, parallel test execution |
| Coverage calculation unreliable | False positive/negative on coverage checks | Low | Use stable coverlet version, validate locally before CI |
| Secrets exposure | Security breach | Low | Use GitHub Secrets, never log sensitive values, rotate keys regularly |
| Cross-platform build failures | Release blocked | Medium | Test locally with same dotnet publish commands, add smoke tests |
| GitHub Actions quota exhausted | CI/CD unavailable | Low (free tier: 2000 min/month) | Monitor usage, optimize workflows, consider paid plan if needed |

---

## Acceptance Criteria Validation

This research resolves all identified unknowns and provides concrete approaches for:

✅ **Test Automation** (FR-001 through FR-005): dotnet test with xUnit, standard GitHub Actions integration

✅ **Coverage Enforcement** (FR-006 through FR-012): coverlet + ReportGenerator with threshold checks and PR comments

✅ **Build Artifacts** (FR-013 through FR-018): dotnet publish with self-contained and single-file options, matrix builds

✅ **Package Publication** (FR-019 through FR-021): Homebrew tap, winget-releaser, Chocolatey API with official actions

✅ **Quality Gates** (FR-026 through FR-030): Branch protection rules, required checks, caching for performance

✅ **Monitoring** (FR-031 through FR-034): PR comments for failures, workflow run logs for audit

✅ **Performance** (NFR-001 through NFR-004): Caching strategy meets timing requirements

✅ **Security** (NFR-008 through NFR-010): GitHub Secrets + Environment protection + CODEOWNERS approval

✅ **Maintainability** (NFR-011 through NFR-013): Version-controlled YAML, DRY via composite actions

---

## Conclusion

All research areas have been thoroughly investigated with concrete technical decisions made. The implementation approach uses industry-standard tools (GitHub Actions marketplace actions, official .NET CLI tooling) aligned with the constitution's simplicity principle. Code signing is appropriately deferred as it's not required for initial package manager distribution. The phased rollout (Homebrew first, then Winget/Chocolatey) manages risk while delivering immediate value.

**Status**: Ready to proceed to Phase 1 (Design & Contracts).

**Next Steps**: Generate workflow contract files in `contracts/` directory, create `data-model.md` for CI/CD entities, write `quickstart.md` for manual testing.
