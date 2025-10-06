# CI/CD Documentation

**Last Updated**: 2025-10-03  
**Status**: Active

## Overview

This document describes the GitHub Actions CI/CD pipeline for Ten Second Tom. The pipeline consists of three workflows that automate testing, building, and distributing the application across multiple platforms.

---

## Required GitHub Configuration

### GitHub Secrets

The following secrets must be configured in the repository settings (`Settings → Secrets and variables → Actions`):

#### For Homebrew Publication (Release Workflow)

**`HOMEBREW_TAP_TOKEN`**
- **Purpose**: Personal access token for pushing formula updates to Homebrew tap repository
- **Scope**: `repo` (Full control of private repositories)
- **Setup**:
  1. Go to GitHub Settings → Developer settings → Personal access tokens → Fine-grained tokens
  2. Click "Generate new token"
  3. Set name: "Homebrew Tap Token for Ten Second Tom"
  4. Set expiration: 1 year (or custom)
  5. Select repository access: Only select repositories → `sirkirby/ten-second-tom-homebrew` (your tap)
  6. Set repository permissions:
     - Contents: Read and write
     - Metadata: Read-only
  7. Generate token and copy immediately (won't be shown again)
  8. Add to repository secrets as `HOMEBREW_TAP_TOKEN`
  9. **Important**: Add the secret to the `production` environment (see Environment Setup below)

#### For Future Package Managers (Phase 2)

**`WINGET_TOKEN`** (Not yet implemented)
- **Purpose**: Token for creating pull requests to microsoft/winget-pkgs repository
- **Status**: Manual process documented in release workflow; automation planned for Phase 2

**`CHOCOLATEY_API_KEY`** (Not yet implemented)
- **Purpose**: API key for publishing packages to chocolatey.org
- **Status**: Manual process documented in release workflow; automation planned for Phase 2

### GitHub Environments

The release workflow uses GitHub Environments to add manual approval gates for production deployments.

#### Production Environment Setup

**`production`**
- **Purpose**: Requires manual approval before publishing to Homebrew
- **Setup**:
  1. Go to repository Settings → Environments
  2. Click "New environment"
  3. Name: `production` (must match exactly)
  4. Click "Configure environment"
  5. **Protection Rules**:
     - ✅ Enable "Required reviewers"
     - Add reviewer(s): Repository maintainers (e.g., @sirkirby)
     - Set wait timer: 0 minutes (immediate notification)
  6. **Environment Secrets**:
     - Add `HOMEBREW_TAP_TOKEN` secret here (same value as repository secret)
  7. Save protection rules

**Workflow Configuration**:
```yaml
publish-homebrew:
  environment:
    name: production
  # Job will pause here until approved
```

**VS Code Linter Warning**: You may see a warning "Value 'production' is not valid" in VS Code. This is expected - the YAML linter cannot access your GitHub repository configuration to validate environment names. The workflow will execute correctly despite this warning.

**Why Use Environments?**
- Prevents accidental publication to Homebrew
- Allows final review of release notes before publishing
- Provides audit trail of who approved each release
- Can be bypassed for hotfixes by maintainers

**Approval Flow**:
1. Release workflow creates GitHub release automatically
2. Workflow pauses at Homebrew publication job
3. GitHub sends notification to configured reviewers
4. Reviewer checks release notes and approves/rejects
5. Upon approval, Homebrew formula is published automatically

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
**Trigger**: Pushes to `main` branch, manual dispatch  
**Purpose**: Build cross-platform self-contained executables

**Jobs**:
1. **Test**: Re-run all tests on main branch to verify integrity
2. **Build macOS x64**: Build self-contained executable for Intel Macs
3. **Build macOS ARM64**: Build self-contained executable for Apple Silicon Macs
4. **Build Windows x64**: Build self-contained executable for Windows
5. **Verify macOS x64**: Smoke test Intel Mac executable with `--version`
6. **Verify macOS ARM64**: Smoke test Apple Silicon executable with `--version`
7. **Verify Windows x64**: Smoke test Windows executable with `--version`

**Performance Targets**:
- Test job: ≤5 minutes
- Each build job: ≤5 minutes (run in parallel)
- Each verify job: <1 minute
- Total: ≤15 minutes

**Build Configuration**:
- **Runtime**: Self-contained (includes .NET runtime)
- **Single File**: Executable + embedded dependencies
- **Trimming**: Assembly-level IL trimming (`TrimMode=link`)
- **Size Target**: <50MB per executable (enforced by workflow)
- **Configuration Files**: `appsettings.json` excluded from single-file bundle
- **Native Libraries**: Included alongside executable (e.g., `libsodium.dylib`)

**Artifacts** (90 days retention):
- `ten-second-tom-osx-x64`: macOS Intel executable + config + native libs
- `ten-second-tom-osx-arm64`: macOS Apple Silicon executable + config + native libs
- `ten-second-tom-win-x64`: Windows executable + config + native libs
- Each artifact includes:
  - Executable file (`tom` or `tom.exe`)
  - Configuration files (`appsettings.json`, `appsettings.Development.json`)
  - Native libraries (`libsodium.dylib` on macOS, DLLs on Windows)
  - SHA256 checksum (in workflow output)

**Platforms**:
- **macOS x64** (`osx-x64`): Intel-based Macs (macOS 10.15+)
- **macOS ARM64** (`osx-arm64`): Apple Silicon Macs (M1/M2/M3+)
- **Windows x64** (`win-x64`): 64-bit Windows (Windows 10+)

**Concurrency**: Uses `github.sha` to cancel duplicate builds for the same commit

**Manual Triggering**: Available via Actions UI for debugging build issues

**Build Process**:
1. Restore NuGet packages (with caching)
2. Publish self-contained executable with single-file bundling
3. Verify executable size (fail if >50MB)
4. Calculate SHA256 checksum
5. Upload artifact with all runtime dependencies
6. Download artifact and run smoke test (`--version`)

**Troubleshooting Build Issues**:
- **Trimming Warnings**: Add `TrimmerRootAssembly` directives for reflection-loaded assemblies
- **Size Violations**: Review dependencies, enable more aggressive trimming
- **Missing Config**: Ensure `ExcludeFromSingleFile=true` for config files
- **Native Library Errors**: Include `*.dylib` (macOS) or `*.dll` (Windows) in artifact upload
- **Smoke Test Failures**: Verify all runtime dependencies are in artifact

---

### 3. Release Workflow

**File**: `.github/workflows/release.yml`  
**Trigger**: Semantic version tags (e.g., `v1.0.0`)  
**Purpose**: Automated distribution to package managers

#### Overview

The release workflow automates the entire distribution process from version tag to package manager publication. It **reuses build artifacts from the build workflow** to avoid redundant builds and enforce proper release hygiene (merge to main → build → tag → release). The workflow validates versions, creates GitHub releases, and publishes to package managers.

#### Jobs

##### 1. Validate Version
- **Purpose**: Ensure tag follows semantic versioning and doesn't duplicate existing releases
- **Steps**:
  1. Extract version from tag (e.g., `v1.2.3` → `1.2.3`)
  2. Validate format matches `MAJOR.MINOR.PATCH` (no pre-release or build metadata)
  3. Query GitHub API to check if version already exists in releases
  4. Output validated version for subsequent jobs
- **Failure Conditions**:
  - Tag doesn't start with 'v'
  - Version format is invalid (e.g., `1.2`, `1.2.3-beta`)
  - Version already exists as a GitHub release
- **Performance Target**: <1 minute

##### 2. Download Build Artifacts
- **Purpose**: Reuse artifacts from the build workflow to avoid redundant builds
- **Dependencies**: Requires validate-version job success
- **Strategy**: Download pre-built artifacts from the build workflow run for this commit
- **Steps**:
  1. Use GitHub CLI to download artifacts from build workflow run
  2. Verify all three platform artifacts exist (osx-x64, osx-arm64, win-x64)
  3. Re-upload artifacts for use by subsequent release jobs
- **Artifacts Reused**:
  - Pre-built executables from build.yml (already tested)
  - SHA256 checksums (calculated during build)
  - Configuration files and native libraries
- **Why Reuse?**:
  - Eliminates 15 minutes of redundant build time
  - Ensures released binaries are identical to tested builds
  - Enforces proper workflow: merge → build → tag → release
  - Reduces CI compute usage and costs
- **Performance Target**: <1 minute (just download, no rebuild)

##### 3. Create GitHub Release
- **Purpose**: Publish official GitHub release with downloadable binaries
- **Dependencies**: Requires build-release-artifacts job success
- **Steps**:
  1. Download all platform artifacts
  2. Generate release notes from git commits since last tag
  3. Create GitHub release with version tag
  4. Upload binaries and checksums as release assets
  5. Mark as latest release
- **Release Assets**:
  - `tom` (macOS Intel binary)
  - `checksums-osx-x64.txt` (SHA256 checksum)
  - `tom` (macOS ARM64 binary)
  - `checksums-osx-arm64.txt` (SHA256 checksum)
  - `tom.exe` (Windows binary)
  - `checksum.txt` (SHA256 checksum)
- **Release Notes**: Auto-generated from commits with link to full changelog
- **Performance Target**: ≤2 minutes

##### 4. Publish to Homebrew
- **Purpose**: Update Homebrew tap with new formula version
- **Dependencies**: Requires create-github-release job success
- **Environment**: `production` (requires manual approval)
- **Approval Gate**: Must be approved by maintainers in CODEOWNERS before execution
- **Steps**:
  1. Wait for manual approval (no time limit)
  2. Clone Homebrew tap repository
  3. Generate formula with version, URLs, and checksums
  4. Commit and push formula to tap repository
  5. Verify formula syntax with `brew audit`
- **Formula Generation**:
  - Version extracted from tag
  - URLs point to GitHub release assets
  - SHA256 checksums from build artifacts
  - Architecture detection (`Hardware::CPU.intel?`)
  - Install block copies binary to `bin/`
  - Test block verifies `--version` command
- **Secrets Required**: `HOMEBREW_TAP_TOKEN` with repo write access
- **Performance Target**: ≤5 minutes (excluding approval wait)

##### 5. Document Winget Release
- **Purpose**: Generate Winget manifest template for manual publication
- **Dependencies**: Requires create-github-release job success
- **Steps**:
  1. Generate `.winget/manifests/` directory structure
  2. Create version manifest with download URLs and checksums
  3. Create installer manifest with silent install flags
  4. Create locale manifest with descriptions
  5. Create GitHub issue with manifest files and publication instructions
- **Status**: Manual process (automation planned for Phase 2)
- **Documentation**: Issue includes step-by-step PR creation guide
- **Performance Target**: <1 minute

##### 6. Document Chocolatey Release
- **Purpose**: Generate Chocolatey package template for manual publication
- **Dependencies**: Requires create-github-release job success
- **Steps**:
  1. Generate `.chocolatey/` directory structure
  2. Create nuspec file with package metadata
  3. Create installation script with download and verification
  4. Create GitHub issue with package files and publication instructions
- **Status**: Manual process (automation planned for Phase 2)
- **Documentation**: Issue includes step-by-step publication guide
- **Performance Target**: <1 minute

#### Concurrency Control

```yaml
concurrency:
  group: release-${{ github.ref }}
  cancel-in-progress: false
```

- **Purpose**: Prevent multiple releases from running simultaneously
- **Group Key**: Uses tag reference (e.g., `refs/tags/v1.0.0`)
- **Cancel Behavior**: Does not cancel in-progress releases
- **Rationale**: Canceling mid-release could corrupt external service publications

#### Performance Summary

**Total Time** (excluding approval):
- Sequential: Validate (2min) → Download (1min) → Release (2min) → Homebrew (5min) → Docs (1min)
- **Target**: ≤10 minutes from tag push to Homebrew availability
- **Actual**: Typically 8-10 minutes (15 minutes faster than building from scratch)
- **Savings**: Reusing build artifacts eliminates ~15 minutes of redundant work

**With Approval**:
- Add human approval time (unlimited, typically minutes to hours)
- Total: ≤30 minutes if approved immediately

#### Approval Process

1. **Merge to Main**: Merge PR to main branch (triggers build.yml automatically)
2. **Wait for Build**: Ensure build workflow completes successfully
3. **Tag Commit**: Tag the merge commit with version (e.g., `git tag v1.0.0 && git push origin v1.0.0`)
4. **Validation**: Workflow validates version, tag location, and finds build artifacts
5. **Download**: Reuses pre-built artifacts from build workflow (no rebuild)
6. **GitHub Release**: Release created with binaries and checksums
7. **Approval Request**: GitHub sends notification to reviewers in CODEOWNERS
8. **Manual Review**: Reviewer checks release notes and approves/rejects
9. **Publication**: Upon approval, Homebrew formula updated automatically
10. **Documentation**: Issues created for manual Winget/Chocolatey publication

#### Package Managers Status

- ✅ **Homebrew**: Fully automated with approval gate
- 📋 **Winget**: Manual publication with generated manifests
- 📋 **Chocolatey**: Manual publication with generated packages

**Phase 2 Plans**: Automate Winget and Chocolatey publication (estimated 2-3 weeks)

---

## Homebrew Tap Setup with Bottle Support

To enable automated Homebrew publication with fast binary installations (bottles), you must create and configure a Homebrew tap repository. The release workflow will automatically create bottles, upload them to GitHub releases, and update the formula.

### What are Homebrew Bottles?

Bottles are pre-compiled binaries that Homebrew can install without building from source. This provides:
- **Fast Installation**: Users download ready-to-use binaries instead of compiling
- **Consistent Builds**: All users get identical binaries
- **Better UX**: No need for build tools or compilation wait time
- **Architecture Support**: Separate bottles for Intel and Apple Silicon Macs

### Prerequisites

- GitHub account (tap repository owner: sirkirby)
- macOS machine for local testing (optional but recommended)
- Homebrew installed locally for testing (`/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"`)

### 1. Create Tap Repository

The tap repository follows Homebrew's standard naming convention:

1. **Create a new GitHub repository**:
   - **Name**: `homebrew-ten-second-tom` (must start with `homebrew-` prefix per Homebrew conventions)
   - **Visibility**: **Public** (required for GitHub Packages and Homebrew)
   - **Description**: "Homebrew tap for Ten Second Tom CLI"
   - **Initialize**: Add a README.md explaining this is a Homebrew tap

2. **Repository URL**: `https://github.com/sirkirby/homebrew-ten-second-tom`

3. **Users will tap it as**: `brew tap sirkirby/ten-second-tom` (Homebrew strips the `homebrew-` prefix automatically)

### 2. Initialize with brew tap-new (Recommended)

Using Homebrew's `brew tap-new` command sets up the proper structure:

```bash
# Create the tap locally with GitHub Packages support
brew tap-new sirkirby/homebrew-ten-second-tom --github-packages

# This creates:
# - Formula/ directory for formula files
# - .github/workflows/ for bottle-building automation (if desired)
# - README.md with tap documentation
# - Proper git configuration

# Push to GitHub
cd $(brew --repository sirkirby/homebrew-ten-second-tom)
gh repo create sirkirby/homebrew-ten-second-tom --public --source=. --push
```

**Note**: The `--github-packages` flag indicates support for GitHub Packages, though our workflow currently uploads bottles to GitHub Releases for simplicity.

### 3. Manual Setup Alternative

If you prefer manual setup or already created the repository:

```bash
# Clone your tap repository
git clone https://github.com/sirkirby/homebrew-ten-second-tom.git
cd homebrew-ten-second-tom

# Create Formula directory
mkdir -p Formula
touch Formula/.gitkeep

# Create README
cat > README.md <<'EOF'
# Ten Second Tom Homebrew Tap

Official Homebrew tap for [Ten Second Tom](https://github.com/sirkirby/ten-second-tom).

## Installation

```bash
brew tap sirkirby/ten-second-tom
brew install ten-second-tom
```

## Usage

```bash
tom --help
```
EOF

# Commit and push
git add .
git commit -m "Initialize Homebrew tap"
git push origin main
```

### 4. Create Initial Formula (Optional)

The release workflow generates the formula automatically, but you can create an initial placeholder:

Create `Formula/ten-second-tom.rb`:

```ruby
class TenSecondTom < Formula
  desc "CLI tool for daily work summaries using Claude AI"
  homepage "https://github.com/sirkirby/ten-second-tom"
  url "https://github.com/sirkirby/ten-second-tom/archive/refs/tags/v0.1.0.tar.gz"
  version "0.1.0"
  license "MIT"

  # Bottles provide fast installation without building from source
  bottle do
    root_url "https://github.com/sirkirby/ten-second-tom/releases/download/v0.1.0"
    sha256 cellar: :any_skip_relocation, arm64_monterey: "PLACEHOLDER"
    sha256 cellar: :any_skip_relocation, monterey: "PLACEHOLDER"
  end

  def install
    bin.install "tom"
  end

  test do
    system "#{bin}/tom", "--version"
  end
end
```

**Important Notes**:
- The `bottle do` block enables fast binary installation for users
- The release workflow automatically generates this with correct checksums
- `cellar: :any_skip_relocation` means the binary is self-contained (no dependencies)
- Separate bottles for Intel (monterey) and Apple Silicon (arm64_monterey)

### 5. Configure Personal Access Token

Create a personal access token with permissions to update the tap repository:

#### Generate Token

1. Go to GitHub Settings → Developer settings → Personal access tokens → **Fine-grained tokens**
2. Click "**Generate new token**"
3. Configure:
   - **Token name**: `Homebrew Tap Token for Ten Second Tom`
   - **Expiration**: 1 year (recommended) or custom
   - **Repository access**: **Only select repositories**
   - Select: `sirkirby/ten-second-tom-homebrew` (your tap repository)
4. Set **Repository permissions**:
   - **Contents**: Read and write (required to push formula updates)
   - **Metadata**: Read-only (automatically included)
5. Click "**Generate token**"
6. **Copy the token immediately** (it won't be shown again)

#### Add Token to Repository Secrets

1. Go to main repository: `https://github.com/sirkirby/ten-second-tom`
2. Navigate to Settings → Secrets and variables → Actions
3. Click "**New repository secret**"
4. Set:
   - **Name**: `HOMEBREW_TAP_TOKEN`
   - **Secret**: Paste the token you generated
5. Click "**Add secret**"

**Important**: Also add this secret to the `production` environment:
1. Go to Settings → Environments → production
2. Scroll to "Environment secrets"
3. Click "Add secret"
4. Add `HOMEBREW_TAP_TOKEN` with the same value

### 6. Test Local Installation

Before releasing, test the tap installation locally:

```bash
# Tap your repository
brew tap sirkirby/ten-second-tom

# Install the formula
brew install ten-second-tom

# Test the installation
tom --version

# Cleanup
brew uninstall ten-second-tom
brew untap sirkirby/ten-second-tom
```

### 7. Understanding Bottles

When users install your formula:

**With Bottles** (after release workflow):
```bash
$ brew install sirkirby/ten-second-tom/ten-second-tom
==> Downloading https://github.com/sirkirby/ten-second-tom/releases/download/v1.0.0/ten-second-tom--1.0.0.arm64_monterey.bottle.tar.gz
==> Pouring ten-second-tom--1.0.0.arm64_monterey.bottle.tar.gz
🍺  /opt/homebrew/Cellar/ten-second-tom/1.0.0: 1 file, 8.5MB
```

**Benefits**:
- Fast installation (seconds instead of minutes)
- No build tools required
- Consistent across all machines
- Architecture-optimized (ARM64 or Intel x64)

**How It Works**:
1. Release workflow creates bottle tarball from pre-built binary
2. Bottle uploaded to GitHub releases alongside source code
3. Formula updated with bottle SHA256 checksums
4. Homebrew downloads bottle instead of building from source
5. User gets instant installation

---
2. Navigate to: Settings → Secrets and variables → Actions
3. Click "**New repository secret**"
4. Configure:
   - **Name**: `HOMEBREW_TAP_TOKEN`
   - **Secret**: Paste the token from previous step
5. Click "**Add secret**"

### 5. Configure GitHub Environment (Production)

The release workflow uses a GitHub Environment to require manual approval before publishing to Homebrew:

1. Go to repository Settings → Environments
2. Click "**New environment**"
3. **Name**: `production` (exact name required)
4. Click "**Configure environment**"
5. Configure settings:
   - ✅ **Required reviewers**: Add `@sirkirby` (or your maintainer team)
   - ✅ **Deployment branches**: Select "**Selected branches**"
     - Add rule: `refs/tags/v*` (allow deployments from version tags)
6. Click "**Save protection rules**"

### 6. Test Installation Locally

After the first release is published (or using a manual test release):

```bash
# Add your tap
brew tap sirkirby/ten-second-tom

# Verify tap was added
brew tap | grep ten-second-tom

# Install from tap
brew install sirkirby/ten-second-tom/ten-second-tom

# Verify installation
ten-second-tom --version

# Update to latest version
brew upgrade ten-second-tom

# Uninstall (cleanup)
brew uninstall ten-second-tom
brew untap sirkirby/ten-second-tom
```

### 7. Formula Structure Details

The release workflow generates formulas with the following structure:

```ruby
class TenSecondTom < Formula
  desc "CLI tool for daily work summaries using Claude AI"
  homepage "https://github.com/sirkirby/ten-second-tom"
  version "1.2.3"  # Extracted from release tag
  license "MIT"

  # Architecture-specific downloads
  if Hardware::CPU.intel?
    url "https://github.com/sirkirby/ten-second-tom/releases/download/v1.2.3/tom"
    sha256 "abc123..."  # macOS x64 SHA256 checksum
  else
    url "https://github.com/sirkirby/ten-second-tom/releases/download/v1.2.3/tom"
    sha256 "def456..."  # macOS ARM64 SHA256 checksum
  end

  def install
    bin.install "tom"
  end

  test do
    system "#{bin}/tom", "--version"
  end
end
```

### 8. Troubleshooting Homebrew Publication

**Problem**: Token authentication failure

**Solution**:
1. Verify `HOMEBREW_TAP_TOKEN` secret exists in repository
2. Check token hasn't expired (regenerate if needed)
3. Verify token has `Contents: Read and write` permission on tap repository
4. Test token manually:
   ```bash
   curl -H "Authorization: token YOUR_TOKEN" \
        https://api.github.com/repos/sirkirby/homebrew-ten-second-tom
   ```

**Problem**: Formula syntax errors

**Solution**:
1. Install Homebrew locally
2. Clone tap repository
3. Test formula syntax:
   ```bash
   brew audit --strict Formula/ten-second-tom.rb
   brew style Formula/ten-second-tom.rb
   ```
4. Test installation:
   ```bash
   brew install --build-from-source Formula/ten-second-tom.rb
   ```

**Problem**: Approval not requested for production environment

**Solution**:
1. Verify "production" environment exists in repository settings
2. Check required reviewers are configured
3. Verify deployment branches include `refs/tags/v*`
4. Check `.github/CODEOWNERS` includes workflow maintainers

**Problem**: Wrong architecture downloaded

**Solution**:
1. Verify formula uses `Hardware::CPU.intel?` check
2. Verify both x64 and ARM64 URLs point to correct binaries
3. Test on both Intel and Apple Silicon Macs if possible

### 9. Homebrew Formula Best Practices

- **Version numbers**: Always use semantic versioning (MAJOR.MINOR.PATCH)
- **Checksums**: Must be SHA256 checksums of downloaded binaries
- **Binary names**: Keep binary name consistent across releases
- **Test block**: Always include a simple test (like `--version`)
- **Description**: Keep concise but descriptive
- **License**: Match the main repository license

### 10. Manual Formula Updates (If Needed)

If automated publication fails, you can manually update the formula:

```bash
# Clone tap repository
git clone https://github.com/sirkirby/ten-second-tom-homebrew.git
cd ten-second-tom-homebrew

# Edit formula
vim Formula/ten-second-tom.rb

# Update version, URLs, and checksums
# Get checksums from GitHub release assets

# Test locally
brew audit --strict Formula/ten-second-tom.rb
brew install --build-from-source Formula/ten-second-tom.rb

# Commit and push
git add Formula/ten-second-tom.rb
git commit -m "Update to version X.Y.Z"
git push origin main

# Users can now update
brew update
brew upgrade ten-second-tom
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

#### Version Validation Failures

**Problem**: Tag format rejected ("Version must start with 'v'")

**Solution**:
```bash
# Check current tag format
git describe --tags --exact-match

# If tag doesn't start with 'v', delete and recreate:
git tag -d 1.0.0
git push origin :refs/tags/1.0.0
git tag v1.0.0
git push origin v1.0.0
```

**Problem**: Tag not on main branch

**Solution**:
The release workflow requires that tags be created on commits that exist on the main branch. This ensures all releases go through the standard quality gates (PR review, tests, builds).

```bash
# Check if your current commit is on main
git branch -r --contains HEAD

# If not on main, merge your branch first:
git checkout main
git pull origin main
git merge your-feature-branch
git push origin main

# Then tag the merge commit:
git tag v1.0.0
git push origin v1.0.0
```

**Problem**: No build artifacts found for commit

**Solution**:
The release workflow reuses artifacts from the build workflow to avoid redundant builds. If no artifacts are found, it means:
1. The commit hasn't been merged to main yet, OR
2. The build workflow hasn't run yet, OR
3. The build workflow failed

**Fix**:
```bash
# 1. Ensure code is merged to main
git checkout main
git pull origin main

# 2. Check if build workflow ran successfully
gh run list --workflow=build.yml --branch=main

# 3. If build failed, fix the issues and merge again

# 4. Wait for build to complete (check Actions tab)

# 5. Then tag the merge commit:
git tag v1.0.0
git push origin v1.0.0
```

**Typical workflow**:
1. Create PR → triggers pr-validation.yml
2. Merge to main → triggers build.yml (creates artifacts)
3. Wait for build success (5-15 minutes)
4. Tag merge commit → triggers release.yml (reuses artifacts)

**Problem**: Invalid semantic version format

**Solution**:
1. Verify format is exactly `vMAJOR.MINOR.PATCH` (e.g., `v1.2.3`)
2. No pre-release identifiers allowed (e.g., `v1.0.0-beta` is invalid)
3. No build metadata allowed (e.g., `v1.0.0+build123` is invalid)
4. Examples:
   - ✅ Valid: `v1.0.0`, `v2.3.4`, `v10.20.30`
   - ❌ Invalid: `v1.0`, `v1.0.0-alpha`, `v1.0.0+123`, `1.0.0` (missing 'v')

**Problem**: Duplicate version error ("Version v1.0.0 already exists")

**Solution**:
1. Check existing releases: `gh release list`
2. If version should be updated:
   ```bash
   # Delete the release
   gh release delete v1.0.0 -y
   
   # Delete the tag locally and remotely
   git tag -d v1.0.0
   git push origin :refs/tags/v1.0.0
   
   # Create new tag and push
   git tag v1.0.0
   git push origin v1.0.0
   ```
3. If version should be incremented:
   ```bash
   # Create next version
   git tag v1.0.1
   git push origin v1.0.1
   ```

#### Build Artifact Failures

**Problem**: Build fails for specific platform

**Solution**:
1. Check job logs for compilation errors
2. Test build locally:
   ```bash
   # macOS x64
   dotnet publish src/TenSecondTom.csproj \
     -c Release \
     -r osx-x64 \
     --self-contained \
     -p:PublishSingleFile=true \
     -p:IncludeNativeLibrariesForSelfExtract=true
   
   # macOS ARM64
   dotnet publish src/TenSecondTom.csproj \
     -c Release \
     -r osx-arm64 \
     --self-contained \
     -p:PublishSingleFile=true
   
   # Windows x64
   dotnet publish src/TenSecondTom.csproj \
     -c Release \
     -r win-x64 \
     --self-contained \
     -p:PublishSingleFile=true
   ```
3. Fix compilation errors and push fix
4. Delete and recreate tag to retrigger workflow

**Problem**: Smoke test fails ("Command '--version' failed")

**Solution**:
1. Download artifact from failed workflow
2. Test executable locally:
   ```bash
   # macOS
   chmod +x ./tom
   ./tom --version
   
   # Windows
   .\tom.exe --version
   ```
3. Check for missing dependencies:
   - macOS: Verify `libsodium.dylib` is included
   - Windows: Verify required DLLs are included
4. Check `appsettings.json` is present and valid
5. Review application startup logs for initialization errors

**Problem**: Checksum calculation fails

**Solution**:
1. Verify executable file exists in publish output
2. Check file permissions allow reading
3. Verify `shasum` (macOS) or `certutil` (Windows) is available
4. Calculate checksum manually to verify:
   ```bash
   # macOS/Linux
   shasum -a 256 TenSecondTom
   
   # Windows PowerShell
   Get-FileHash TenSecondTom.exe -Algorithm SHA256
   ```

#### GitHub Release Failures

**Problem**: Release creation fails ("Resource not accessible")

**Solution**:
1. Verify repository permissions (workflow has write access by default)
2. Check if release with same tag exists: `gh release list`
3. Delete existing release if needed: `gh release delete v1.0.0`
4. Verify artifact downloads succeeded in previous job
5. Check GitHub API status: https://www.githubstatus.com/

**Problem**: Asset upload fails

**Solution**:
1. Check artifact file size (GitHub limit: 2GB per file)
2. Verify artifact download completed successfully
3. Check file exists in workflow directory:
   ```yaml
   - name: List downloaded artifacts
     run: ls -lah release-*/
   ```
4. Retry upload or use `gh release upload` with `--clobber` flag

**Problem**: Release notes generation fails

**Solution**:
1. Verify git history exists between tags
2. Check previous tag exists: `git tag | sort -V`
3. Generate release notes manually:
   ```bash
   # Get commits since last tag
   git log $(git describe --tags --abbrev=0 HEAD^)..HEAD --oneline
   
   # Use gh CLI
   gh release create v1.0.0 --generate-notes
   ```

#### Homebrew Publication Failures

**Problem**: Authentication failure ("Resource not accessible by personal access token")

**Solution**:
1. Verify `HOMEBREW_TAP_TOKEN` secret exists:
   - Go to repository Settings → Secrets and variables → Actions
   - Confirm `HOMEBREW_TAP_TOKEN` is listed
2. Check token hasn't expired:
   - Go to GitHub Settings → Developer settings → Personal access tokens
   - Check expiration date, regenerate if expired
3. Verify token permissions:
   - Required: `Contents: Read and write` on tap repository
   - Test token manually:
     ```bash
     curl -H "Authorization: token YOUR_TOKEN" \
          https://api.github.com/repos/sirkirby/homebrew-ten-second-tom
     ```
4. Regenerate token if needed and update secret

**Problem**: Formula syntax errors ("brew audit failed")

**Solution**:
1. Clone tap repository locally
2. Test formula syntax:
   ```bash
   brew audit --strict Formula/ten-second-tom.rb
   brew style Formula/ten-second-tom.rb
   ```
3. Common issues:
   - Invalid Ruby syntax (check brackets, quotes, indentation)
   - Incorrect SHA256 format (must be 64 hex characters)
   - Invalid URL format
   - Missing required fields (desc, homepage, url, sha256)
4. Fix formula in tap repository manually if needed
5. Update workflow formula template if issue is in generation logic

**Problem**: Approval not requested (deployment doesn't wait)

**Solution**:
1. Verify GitHub Environment exists:
   - Go to repository Settings → Environments
   - Confirm "production" environment exists
2. Check environment configuration:
   - Required reviewers: Must include at least one reviewer
   - Deployment branches: Must include `refs/tags/v*` pattern
3. Verify CODEOWNERS configuration:
   ```bash
   cat .github/CODEOWNERS
   # Should include: /.github/workflows/release.yml @sirkirby
   ```
4. Check job environment declaration:
   ```yaml
   publish-homebrew:
     environment: production  # Must match environment name exactly
   ```

**Problem**: Formula installation fails for users

**Solution**:
1. Test installation locally:
   ```bash
   brew tap sirkirby/ten-second-tom
   brew install --verbose sirkirby/ten-second-tom/ten-second-tom
   ```
2. Check formula downloads correct binaries:
   - Intel Macs should download `osx-x64` binary
   - Apple Silicon Macs should download `osx-arm64` binary
3. Verify checksums match:
   ```bash
   # Download binary manually
   curl -L -o TenSecondTom https://github.com/sirkirby/ten-second-tom/releases/download/v1.0.0/TenSecondTom-osx-x64
   shasum -a 256 TenSecondTom
   # Compare with SHA256 in formula
   ```
4. Test binary works:
   ```bash
   chmod +x TenSecondTom
   ./TenSecondTom --version
   ```

**Problem**: Tap repository push fails ("Updates were rejected")

**Solution**:
1. Check for concurrent releases (concurrency group should prevent this)
2. Verify tap repository hasn't been force-pushed
3. Try pulling latest changes before pushing:
   ```yaml
   - name: Pull latest changes
     run: |
       cd homebrew-ten-second-tom
       git pull --rebase origin main
       git push origin main
   ```

#### Package Manager Documentation Failures

**Problem**: Issue creation fails for Winget/Chocolatey

**Solution**:
1. Verify workflow has `issues: write` permission
2. Check GitHub API rate limits: https://api.github.com/rate_limit
3. Create issue manually with generated manifest content
4. Verify issue template renders correctly (check Markdown syntax)

**Problem**: Generated manifests are invalid

**Solution**:
1. Validate Winget manifest:
   ```bash
   # Install winget-create tool
   winget install Microsoft.WingetCreate
   
   # Validate manifest
   wingetcreate validate --manifest .winget/manifests/
   ```
2. Validate Chocolatey package:
   ```bash
   # Install Chocolatey
   Set-ExecutionPolicy Bypass -Scope Process -Force; 
   [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; 
   iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
   
   # Test package
   choco pack .chocolatey/ten-second-tom.nuspec
   choco install ten-second-tom -source . -y
   ```

#### Concurrency Issues

**Problem**: Multiple releases running simultaneously

**Solution**:
1. Verify concurrency configuration in workflow:
   ```yaml
   concurrency:
     group: release-${{ github.ref }}
     cancel-in-progress: false
   ```
2. Check workflow runs for same tag:
   ```bash
   gh run list --workflow=release.yml --branch=v1.0.0
   ```
3. Cancel duplicate runs manually:
   ```bash
   gh run cancel <run-id>
   ```
4. Wait for first run to complete before retriggering

**Problem**: Release stuck "Waiting for approval"

**Solution**:
1. Check who can approve:
   - Go to workflow run page
   - Click "Review deployments" button
   - Verify your GitHub account is listed as required reviewer
2. If not listed, update environment configuration:
   - Settings → Environments → production → Required reviewers
3. Approve deployment:
   - Click "Review deployments" button
   - Select "production" environment
   - Click "Approve and deploy"

#### Performance Issues

**Problem**: Performance target exceeded (>10 minutes excluding approval)

**Solution**:
The release workflow should complete in ~10 minutes by reusing build artifacts. If it's slower:

1. **Check artifact download time**:
   - Look at "Download Build Artifacts" job timing
   - Large artifacts or slow network can cause delays
   - Artifacts are typically 30-50MB total

2. **Check GitHub Release creation time**:
   - Look at "Create GitHub Release" job timing
   - Release note generation should be <30 seconds
   - Asset upload should be <1 minute

3. **Check Homebrew publication time**:
   - Look at "Publish to Homebrew" job timing (excluding approval wait)
   - Git clone/push should be <1 minute
   - If slow, check tap repository size

4. **Verify no builds are happening**:
   - Release workflow should NOT rebuild executables
   - If you see "dotnet publish" commands, the refactor didn't work
   - All executables should come from pre-built artifacts

**Expected job timings**:
- validate-version: 1-2 minutes
- download-artifacts: 30-60 seconds
- create-github-release: 1-2 minutes
- publish-homebrew: 2-5 minutes (excluding approval)
- document-*: 10-30 seconds each

**If builds are happening** (wrong!):
- Check that the workflow is actually downloading artifacts
- Verify `needs: validate-version` outputs are correct
- Check GitHub Actions logs for "Found build workflow run" message

**Solution**:
1. Identify slow job(s) in workflow timeline
2. Common slow jobs:
   - Build artifacts: Check if NuGet cache is being used
   - Homebrew publication: May be waiting for approval
3. Optimize slow steps:
   ```yaml
   # Improve cache hit rate
   - uses: actions/cache@v3
     with:
       path: ~/.nuget/packages
       key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
       restore-keys: |
         ${{ runner.os }}-nuget-
   ```
4. Consider parallel execution where possible (already implemented for builds)

**Problem**: Build artifacts job timeout (>15 minutes)

**Solution**:
1. Check if builds are running in parallel (matrix strategy)
2. Verify NuGet cache is working:
   ```yaml
   - name: Check cache hit
     run: echo "Cache hit: ${{ steps.cache.outputs.cache-hit }}"
   ```
3. Reduce dependencies or enable more aggressive trimming
4. Check GitHub Actions runner performance (may be throttled)

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
