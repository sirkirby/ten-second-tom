# Homebrew Bottles Implementation Update

**Date**: 2025-10-06  
**Status**: Implemented - Ready for Testing  
**Related Tasks**: T042, T046, T050, T051

## Overview

Updated the Homebrew tap implementation to properly support **bottles** (pre-compiled binaries) following Homebrew best practices. This provides users with fast, consistent installations without requiring build-from-source compilation.

## Changes Made

### 1. Repository Naming (Standard Homebrew Convention)

**Repository Name**: `homebrew-ten-second-tom` (with `homebrew-` **prefix**)

**Rationale**: Following the official Homebrew naming convention. From the Homebrew documentation:

> "If hosted on GitHub, we recommend that the repository's name start with `homebrew-` so the short `brew tap` command can be used."

When users tap the repository, Homebrew automatically strips the `homebrew-` prefix, so:
- **Repository**: `sirkirby/homebrew-ten-second-tom`
- **User command**: `brew tap sirkirby/ten-second-tom` (prefix stripped automatically)
- **Installation**: `brew install sirkirby/ten-second-tom/ten-second-tom` or just `brew install ten-second-tom` (after tapping)

**User Impact**:
```bash
# Tap the repository (Homebrew strips homebrew- prefix)
brew tap sirkirby/ten-second-tom

# Install the formula
brew install ten-second-tom

# Or use full path without tapping first
brew install sirkirby/ten-second-tom/ten-second-tom
```

### 2. Bottle Support in Release Workflow

**File**: `.github/workflows/release.yml`

**Changes**:
- Changed `publish-homebrew` job runner from `ubuntu-latest` to `macos-latest` to enable bottle creation
- Added `permissions.packages: write` for GitHub Packages support
- Added bottle creation step:
  - Creates proper Homebrew bottle tarball structure
  - Calculates SHA256 checksums for bottles
  - Detects macOS version for proper bottle tagging (monterey, ventura, sonoma, sequoia)
  - Supports both Intel (x64) and Apple Silicon (ARM64) architectures
- Updated formula generation to include `bottle do...end` blocks
- Bottles uploaded to GitHub Releases for distribution
- Formula references bottle root_url pointing to release assets

**Formula Structure** (Generated):
```ruby
class TenSecondTom < Formula
  desc "CLI tool for daily work summaries using Claude AI"
  homepage "https://github.com/sirkirby/ten-second-tom"
  url "https://github.com/sirkirby/ten-second-tom/archive/refs/tags/v1.0.0.tar.gz"
  version "1.0.0"
  license "MIT"

  # Bottles provide fast installation without building from source
  bottle do
    root_url "https://github.com/sirkirby/ten-second-tom/releases/download/v1.0.0"
    sha256 cellar: :any_skip_relocation, arm64_monterey: "abc123..."
    sha256 cellar: :any_skip_relocation, monterey: "def456..."
  end

  def install
    bin.install "tom"
  end

  test do
    system "#{bin}/tom", "--version"
  end
end
```

### 3. Documentation Updates

**File**: `docs/CICD.md`

**Additions**:
- New section: "What are Homebrew Bottles?" explaining benefits
- Updated tap creation instructions to use `brew tap-new --github-packages`
- Added bottle-specific installation examples
- Updated formula template to include bottle blocks
- Expanded troubleshooting for bottle-related issues
- Added performance comparison: bottles vs source builds

**Key Benefits Documented**:
- Fast Installation: Seconds instead of minutes
- No Build Tools Required: Users don't need Xcode or compilers
- Consistent Builds: All users get identical binaries
- Architecture Optimized: Native ARM64 or Intel x64

### 4. Research Documentation Update

**File**: `specs/002-as-per-the/research.md`

**Changes**:
- Completely rewrote Homebrew publication section
- Added detailed bottle format explanation
- Documented GitHub Packages integration (for future use)
- Updated authentication requirements
- Added bottle naming conventions
- Explained tap naming with `brew tap-new`

### 5. Tasks Update

**File**: `specs/002-as-per-the/tasks.md`

**Updated Tasks**:
- **T042**: Added bottle upload and GitHub Packages references
- **T046**: Added bottle documentation requirements
- **T050**: Expanded testing steps to verify bottle installation
  - Added verification that installation uses bottles ("Pouring" message)
  - Updated tap repository name in prerequisites
  - Added GitHub Packages visibility check

## What's New for Users

### Fast Installation Experience

**Before** (raw binary download):
```bash
$ brew install sirkirby/homebrew-ten-second-tom/ten-second-tom
==> Downloading https://github.com/sirkirby/ten-second-tom/releases/download/v1.0.0/tom
==> Caveats
This is a pre-built binary, not a bottle...
```

**After** (bottle installation):
```bash
$ brew install sirkirby/ten-second-tom/ten-second-tom
==> Downloading https://github.com/sirkirby/ten-second-tom/releases/download/v1.0.0/ten-second-tom--1.0.0.arm64_monterey.bottle.tar.gz
==> Pouring ten-second-tom--1.0.0.arm64_monterey.bottle.tar.gz
🍺  /opt/homebrew/Cellar/ten-second-tom/1.0.0: 1 file, 8.5MB
```

### Installation Performance

- **Source Build**: 5-10 minutes (with build tools)
- **Raw Binary**: 10-30 seconds (download only)
- **Bottle**: 5-10 seconds (optimized for Homebrew)

## Technical Details

### Bottle Creation Process

1. **Artifact Download**: Reuses pre-built binaries from build workflow
2. **Directory Structure**: Creates Homebrew-compliant structure:
   ```
   ten-second-tom/
     {version}/
       bin/
         tom
   ```
3. **Tarball Creation**: Packages as gzipped tarball with proper naming
4. **Checksum Calculation**: Generates SHA256 for formula validation
5. **Upload**: Attaches to GitHub Release as release asset

### macOS Version Detection

The workflow automatically detects the runner's macOS version and tags bottles appropriately:
- macOS 12 → `monterey` / `arm64_monterey`
- macOS 13 → `ventura` / `arm64_ventura`
- macOS 14 → `sonoma` / `arm64_sonoma`
- macOS 15 → `sequoia` / `arm64_sequoia`

This ensures compatibility with Homebrew's version-specific bottle system.

### Why GitHub Releases Instead of GitHub Packages?

**Decision**: Use GitHub Releases for bottle hosting initially.

**Rationale**:
1. **Simplicity**: GitHub Releases is simpler to set up and use
2. **Public Access**: No authentication required for downloads
3. **Integrated**: Bottles stored alongside source releases
4. **Proven**: Standard approach for many Homebrew taps

**Future**: Can migrate to GitHub Packages (ghcr.io) if needed for:
- Better CDN distribution
- Package-specific analytics
- Separate versioning from releases

## Testing Checklist

Before marking T050 complete, verify:

- [ ] Tap repository created: `sirkirby/homebrew-ten-second-tom` (with `homebrew-` prefix)
- [ ] Repository is public (required for bottles)
- [ ] Formula directory structure exists
- [ ] HOMEBREW_TAP_TOKEN secret configured
- [ ] Production environment set up with approval
- [ ] Release workflow creates bottles successfully
- [ ] Bottles uploaded to GitHub Release assets
- [ ] Formula includes bottle blocks with correct checksums
- [ ] `brew tap sirkirby/ten-second-tom` works (prefix automatically stripped)
- [ ] `brew install ten-second-tom` uses bottles (shows "Pouring" message)
- [ ] Installed binary runs correctly
- [ ] Both architectures supported (Intel and Apple Silicon)

## Migration Notes

**No migration required** - This is the initial implementation. The repository name `homebrew-ten-second-tom` follows the standard Homebrew convention with the `homebrew-` prefix.

## References

- [Homebrew: How to Create and Maintain a Tap](https://docs.brew.sh/How-to-Create-and-Maintain-a-Tap)
- [Homebrew: Formula Cookbook](https://docs.brew.sh/Formula-Cookbook)
- [Homebrew: Bottles](https://docs.brew.sh/Bottles)
- GitHub Actions: `.github/workflows/release.yml` (updated)
- Documentation: `docs/CICD.md` (Homebrew Tap Setup section)

## Next Steps

1. **T050**: Test release workflow manually with bottle creation
2. **T051**: Verify performance targets are met
3. **Future**: Consider migrating to GitHub Packages if benefits outweigh complexity

---

**Implementation Status**: ✅ Complete - Ready for Testing  
**Version**: Spec 002 Phase 3.13  
**Last Updated**: 2025-10-06
