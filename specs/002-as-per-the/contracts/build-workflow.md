# Contract: Build Workflow

**File**: `.github/workflows/build.yml`  
**Purpose**: Build cross-platform executables on main branch merges  
**Trigger**: Push to `main` branch

---

## Workflow Interface

### Inputs
- **Event**: `push` to `main` branch
- **Source**: Merged pull request or direct commit

### Outputs
- **Build Artifacts**: Self-contained executables for macOS (x64, ARM64) and Windows (x64)
- **Artifact Metadata**: Version, commit SHA, build timestamp, checksums
- **Build Status**: Pass/fail for each platform

### Exit Conditions
- **Success**: All platform builds succeed, smoke tests pass, artifacts uploaded
- **Failure**: Any platform build fails, smoke test fails, or artifact size exceeds 50MB

---

## Jobs

### Job 1: Test

**Purpose**: Re-run tests to verify main branch integrity

**Runner**: `ubuntu-latest`

**Steps**:
1. Checkout code from main branch
2. Setup .NET 9 SDK with NuGet caching
3. Restore dependencies
4. Build with Release configuration
5. Run all tests with xUnit
6. Fail if any tests fail

**Inputs**:
- Source code from main branch
- .NET 9 SDK
- NuGet packages

**Outputs**:
- Test result summary (must be all passing)
- Build status

**Performance Target**: ≤5 minutes

**Contract**:
```yaml
Preconditions:
  - Main branch code (just merged)
  - All PR checks passed previously
  - Valid C# code in src/

Postconditions:
  - All tests pass (verified on main)
  - Code builds without errors/warnings
  - Ready for platform-specific builds

Failure Modes:
  - Tests fail → abort entire workflow, alert maintainers
  - Build fails → abort workflow with diagnostic info
```

---

### Job 2: Build macOS x64

**Purpose**: Build self-contained executable for macOS x64

**Runner**: `macos-latest`

**Dependencies**: Job 1 (Test)

**Steps**:
1. Checkout code
2. Setup .NET 9 SDK
3. Publish with `dotnet publish` for `osx-x64` runtime
   - Configuration: Release
   - Self-contained: true
   - Single file: true
   - Trimmed: true
4. Verify output size <50MB
5. Upload artifact with metadata

**Inputs**:
- Source code from main
- .NET 9 SDK for macOS
- Runtime identifier: `osx-x64`

**Outputs**:
- Executable: `ten-second-tom` (macOS x64)
- Size: <50MB
- Artifact name: `ten-second-tom-osx-x64-{version}`
- Metadata: version, SHA256 checksum, build timestamp

**Performance Target**: ≤5 minutes

**Contract**:
```yaml
Preconditions:
  - Test job succeeded
  - macOS runner available
  - .NET 9 SDK for macOS

Postconditions:
  - Self-contained executable exists
  - File size < 50MB (FR-018)
  - File is executable (permissions set)
  - Artifact uploaded to GitHub Actions

Failure Modes:
  - Publish fails → fail with error details
  - Size exceeds 50MB → fail with size info, suggest trimming
  - Upload fails → fail with artifact error
```

---

### Job 3: Build macOS ARM64

**Purpose**: Build self-contained executable for macOS ARM64 (Apple Silicon)

**Runner**: `macos-latest`

**Dependencies**: Job 1 (Test)

**Steps**:
1. Checkout code
2. Setup .NET 9 SDK
3. Publish with `dotnet publish` for `osx-arm64` runtime
   - Configuration: Release
   - Self-contained: true
   - Single file: true
   - Trimmed: true
4. Verify output size <50MB
5. Upload artifact with metadata

**Inputs**:
- Source code from main
- .NET 9 SDK for macOS
- Runtime identifier: `osx-arm64`

**Outputs**:
- Executable: `ten-second-tom` (macOS ARM64)
- Size: <50MB
- Artifact name: `ten-second-tom-osx-arm64-{version}`
- Metadata: version, SHA256 checksum, build timestamp

**Performance Target**: ≤5 minutes

**Contract**: (Same as Job 2, different RID)

---

### Job 4: Build Windows x64

**Purpose**: Build self-contained executable for Windows x64

**Runner**: `windows-latest`

**Dependencies**: Job 1 (Test)

**Steps**:
1. Checkout code
2. Setup .NET 9 SDK
3. Publish with `dotnet publish` for `win-x64` runtime
   - Configuration: Release
   - Self-contained: true
   - Single file: true
   - Trimmed: true
4. Verify output size <50MB
5. Upload artifact with metadata

**Inputs**:
- Source code from main
- .NET 9 SDK for Windows
- Runtime identifier: `win-x64`

**Outputs**:
- Executable: `ten-second-tom.exe` (Windows x64)
- Size: <50MB
- Artifact name: `ten-second-tom-win-x64-{version}`
- Metadata: version, SHA256 checksum, build timestamp

**Performance Target**: ≤5 minutes

**Contract**:
```yaml
Preconditions:
  - Test job succeeded
  - Windows runner available
  - .NET 9 SDK for Windows

Postconditions:
  - Self-contained executable exists
  - File size < 50MB (FR-018)
  - File has .exe extension
  - Artifact uploaded to GitHub Actions

Failure Modes:
  - Publish fails → fail with error details
  - Size exceeds 50MB → fail with size info, suggest trimming
  - Upload fails → fail with artifact error
```

---

### Job 5: Verify macOS x64

**Purpose**: Smoke test macOS x64 executable

**Runner**: `macos-latest`

**Dependencies**: Job 2 (Build macOS x64)

**Steps**:
1. Download macOS x64 artifact
2. Set executable permissions
3. Run smoke test: `./ten-second-tom --version`
4. Verify exit code 0 and version output

**Inputs**:
- macOS x64 artifact from Job 2

**Outputs**:
- Verification status (pass/fail)
- Version output captured

**Performance Target**: <1 minute

**Contract**:
```yaml
Preconditions:
  - Build macOS x64 succeeded
  - Artifact available for download

Postconditions:
  - Executable runs without crash
  - Version command succeeds (exit code 0)
  - Version output contains semver string

Failure Modes:
  - Executable doesn't run → fail with OS error
  - Version command fails → fail with exit code
  - No version output → fail with missing output error
```

---

### Job 6: Verify macOS ARM64

**Purpose**: Smoke test macOS ARM64 executable

**Runner**: `macos-latest`

**Dependencies**: Job 3 (Build macOS ARM64)

**Contract**: (Same as Job 5, different artifact)

---

### Job 7: Verify Windows x64

**Purpose**: Smoke test Windows x64 executable

**Runner**: `windows-latest`

**Dependencies**: Job 4 (Build Windows x64)

**Steps**:
1. Download Windows x64 artifact
2. Run smoke test: `.\ten-second-tom.exe --version`
3. Verify exit code 0 and version output

**Contract**: (Same as Job 5, Windows-specific)

---

## Performance Constraints

- **Total Workflow Time**: ≤15 minutes (NFR-003)
- **Individual Job Time**: ≤5 minutes per build job
- **Parallel Execution**: Build jobs (2-4) run in parallel after test job

---

## Artifact Specifications

### Artifact Naming Convention
- `ten-second-tom-{platform}-{arch}-{version}`
- Examples:
  - `ten-second-tom-osx-x64-1.2.3`
  - `ten-second-tom-osx-arm64-1.2.3`
  - `ten-second-tom-win-x64-1.2.3`

### Artifact Metadata (JSON)
```json
{
  "version": "1.2.3",
  "commit_sha": "abc123...",
  "build_timestamp": "2025-10-03T12:34:56Z",
  "platform": "osx-x64",
  "file_name": "ten-second-tom",
  "file_size_bytes": 45678901,
  "sha256_checksum": "def456..."
}
```

### Artifact Retention
- GitHub Actions artifacts: 90 days
- Used by release workflow for GitHub Releases (indefinite retention)

---

## Success Criteria

✅ All three platform builds complete successfully  
✅ All executables <50MB in size  
✅ All smoke tests pass (executables run with --version)  
✅ All artifacts uploaded with correct metadata  
✅ Workflow completes in ≤15 minutes  
✅ Build artifacts available for download and release workflow
