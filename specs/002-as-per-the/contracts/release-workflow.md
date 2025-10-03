# Contract: Release Workflow

**File**: `.github/workflows/release.yml`  
**Purpose**: Automate releases and package publication on version tags  
**Trigger**: Push of semantic version tag (e.g., `v1.2.3`)

---

## Workflow Interface

### Inputs
- **Event**: `push` with tag matching pattern `v*.*.*` (semantic version)
- **Tag Format**: `v{MAJOR}.{MINOR}.{PATCH}` (e.g., `v1.2.3`)

### Outputs
- **GitHub Release**: Created with release notes and binaries attached
- **Package Publications**: Homebrew formula updated (Phase 1)
- **Publication Status**: Success/failure for each package manager

### Exit Conditions
- **Success**: GitHub release created, all enabled package managers published
- **Failure**: Version invalid, release creation fails, or package publication fails

---

## Jobs

### Job 1: Validate Version

**Purpose**: Verify tag follows semantic versioning and doesn't already exist

**Runner**: `ubuntu-latest`

**Steps**:
1. Extract version from tag (remove `v` prefix)
2. Validate semantic version format (MAJOR.MINOR.PATCH)
3. Check that version doesn't exist in GitHub releases
4. Check that version doesn't exist in package managers
5. Fail if validation fails

**Inputs**:
- Git tag from trigger event

**Outputs**:
- Validated version string (e.g., "1.2.3")
- Validation status (pass/fail)

**Performance Target**: <1 minute

**Contract**:
```yaml
Preconditions:
  - Tag pushed to repository
  - Tag name starts with 'v'

Postconditions:
  - Version follows semver (MAJOR.MINOR.PATCH)
  - Version is unique (not previously released)
  - Version number extracted and validated

Failure Modes:
  - Invalid semver format → fail with format error
  - Version already exists → fail with duplicate error
  - Missing version → fail with extraction error
```

---

### Job 2: Build Release Artifacts

**Purpose**: Build production executables for all platforms

**Runner**: Matrix (ubuntu-latest, macos-latest, windows-latest)

**Dependencies**: Job 1 (Validate Version)

**Steps**:
1. Checkout code at tag
2. Setup .NET 9 SDK
3. Build executables for all platforms:
   - macOS x64 (self-contained, single-file, trimmed)
   - macOS ARM64 (self-contained, single-file, trimmed)
   - Windows x64 (self-contained, single-file, trimmed)
4. Verify each executable <50MB
5. Run smoke tests on each
6. Calculate SHA256 checksums
7. Upload artifacts with metadata

**Inputs**:
- Source code at version tag
- Version string from Job 1

**Outputs**:
- Three platform executables
- Checksum file (SHA256 for each binary)
- Build metadata

**Performance Target**: ≤15 minutes (parallel execution)

**Contract**:
```yaml
Preconditions:
  - Version validated
  - Code at tag is clean and builds
  - .NET 9 SDK available on all runners

Postconditions:
  - All executables built successfully (FR-013, FR-014)
  - All executables verified via smoke test (FR-015)
  - All executables <50MB (FR-018)
  - All checksums calculated
  - Artifacts include runtime dependencies (FR-017)

Failure Modes:
  - Build fails → abort release with error details
  - Smoke test fails → abort release with test output
  - Size exceeds limit → abort with size info
```

---

### Job 3: Create GitHub Release

**Purpose**: Create GitHub release with release notes and binaries

**Runner**: `ubuntu-latest`

**Dependencies**: Job 2 (Build Release Artifacts)

**Steps**:
1. Download all build artifacts
2. Generate release notes from commits since last tag
3. Create GitHub release with version tag
4. Attach all executables to release
5. Attach checksum file to release
6. Publish release (make visible)

**Inputs**:
- Build artifacts from Job 2
- Version tag and commit history

**Outputs**:
- GitHub Release URL
- Release ID for subsequent jobs

**Performance Target**: ≤2 minutes

**Contract**:
```yaml
Preconditions:
  - All artifacts built successfully
  - GitHub token has release permissions
  - Tag exists in repository

Postconditions:
  - GitHub release created (FR-024)
  - Release notes generated automatically
  - All binaries attached to release
  - Checksums included for verification
  - Release publicly visible

Failure Modes:
  - Release creation fails → abort with API error
  - Artifact upload fails → retry, then abort
  - Notes generation fails → use default template
```

---

### Job 4: Publish to Homebrew

**Purpose**: Update Homebrew tap with new formula version

**Runner**: `ubuntu-latest`

**Dependencies**: Job 3 (Create GitHub Release)

**Steps**:
1. Download macOS binaries from release
2. Calculate SHA256 checksums
3. Generate/update Homebrew formula
4. Push formula to tap repository
5. Verify formula syntax
6. Test installation locally (if runner supports)

**Inputs**:
- macOS x64 and ARM64 binaries from release
- Version string
- Release URL

**Outputs**:
- Updated Homebrew formula in tap repository
- Formula verification status

**Performance Target**: ≤5 minutes

**Contract**:
```yaml
Preconditions:
  - GitHub release exists with macOS binaries
  - HOMEBREW_TAP_TOKEN secret configured
  - Tap repository exists (e.g., sirkirby/homebrew-ten-second-tom)

Postconditions:
  - Homebrew formula updated with new version (FR-019)
  - Formula includes both x64 and ARM64 binaries
  - Formula syntax validated
  - Formula pushed to tap repository

Failure Modes:
  - Token invalid → fail with auth error
  - Formula syntax error → fail with validation error
  - Push fails → retry, then fail with error
```

---

### Job 5: Approval Gate (Manual)

**Purpose**: Require manual approval before production package publication

**Runner**: N/A (GitHub Environment approval)

**Dependencies**: Job 4 (Homebrew published)

**Steps**:
1. Wait for manual approval from CODEOWNERS
2. Timeout after 7 days
3. Proceed to Winget/Chocolatey publication (Phase 2)

**Inputs**:
- Release validation results
- CODEOWNERS configuration

**Outputs**:
- Approval granted/denied
- Approver identity logged

**Performance Target**: Manual (human-driven)

**Contract**:
```yaml
Preconditions:
  - GitHub release created
  - Homebrew publication succeeded
  - CODEOWNERS file exists and valid (FR-025)

Postconditions:
  - Authorized team member approved release
  - Approval logged in workflow run
  - Ready for wider distribution

Failure Modes:
  - Timeout (7 days) → abort remaining publications
  - Approval denied → abort workflow
  - No CODEOWNERS configured → fail with error
```

---

### Job 6: Document Release (Future: Winget)

**Purpose**: Document Winget publication process for Phase 2

**Runner**: `ubuntu-latest`

**Dependencies**: Job 5 (Approval Gate)

**Steps**:
1. Generate Winget manifest YAML
2. Document required manual steps
3. Create issue for manual Winget publication
4. Link to microsoft/winget-pkgs fork

**Note**: Phase 1 documents process; Phase 2 automates publication

**Contract**:
```yaml
Preconditions:
  - Approval granted
  - Release artifacts available

Postconditions:
  - Winget manifest generated (FR-020 - partial)
  - Manual publication steps documented
  - GitHub issue created for tracking

Failure Modes:
  - Manifest generation fails → warn, don't abort
```

---

### Job 7: Document Release (Future: Chocolatey)

**Purpose**: Document Chocolatey publication process for Phase 2

**Runner**: `ubuntu-latest`

**Dependencies**: Job 5 (Approval Gate)

**Steps**:
1. Generate Chocolatey nuspec file
2. Document required manual steps
3. Create issue for manual Chocolatey publication

**Note**: Phase 1 documents process; Phase 2 automates publication

**Contract**:
```yaml
Preconditions:
  - Approval granted
  - Release artifacts available

Postconditions:
  - Chocolatey nuspec generated (FR-021 - partial)
  - Manual publication steps documented
  - GitHub issue created for tracking

Failure Modes:
  - Nuspec generation fails → warn, don't abort
```

---

## Performance Constraints

- **Total Workflow Time**: ≤30 minutes (NFR-004) excluding manual approval
- **Automated Steps**: ≤25 minutes
- **Manual Approval**: Up to 7 days timeout

---

## Security & Approval

### Required Secrets
- `GITHUB_TOKEN`: GitHub release creation (automatic)
- `HOMEBREW_TAP_TOKEN`: Homebrew tap repository access (manual configuration)
- `WINGET_GITHUB_TOKEN`: Winget fork/PR creation (Phase 2)
- `CHOCOLATEY_API_KEY`: Chocolatey publication (Phase 2)

### CODEOWNERS Configuration
```
# Release approval
/.github/workflows/release.yml @maintainer-team
/releases/ @maintainer-team
```

### Environment Protection Rules
- Environment: `production`
- Required reviewers: Team specified in CODEOWNERS
- Deployment branches: Tags only (`v*`)

---

## Package Manager Requirements

### Homebrew (Phase 1)
- ✅ Tap repository created
- ✅ Formula template prepared
- ✅ Token configured
- ✅ Automated publication

### Winget (Phase 2 - Future)
- ⏳ Fork microsoft/winget-pkgs
- ⏳ Automate manifest generation
- ⏳ Automate PR creation
- ⏳ Configure token

### Chocolatey (Phase 2 - Future)
- ⏳ Chocolatey account created
- ⏳ Automate package creation
- ⏳ Automate publication
- ⏳ Configure API key

---

## Success Criteria

✅ Tag triggers release workflow automatically  
✅ Version validated as semantic version (FR-022)  
✅ Version uniqueness verified (FR-023)  
✅ GitHub release created with all binaries (FR-024)  
✅ Homebrew tap updated automatically (FR-019)  
✅ Manual approval required for production (FR-025, NFR-010)  
✅ Workflow completes in ≤30 minutes (NFR-004)  
✅ Winget and Chocolatey processes documented (FR-020, FR-021 - Phase 1 documentation only)
