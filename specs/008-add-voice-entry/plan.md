# Implementation Plan: Voice Entry with Local-First Speech-to-Text

**Branch**: `008-add-voice-entry` | **Date**: 2025-10-20 | **Spec**: [spec.md](./spec.md)  
**Input**: Feature specification from `/specs/008-add-voice-entry/spec.md`

## Summary

Add voice entry capability to Ten Second Tom with local-first speech-to-text using whisper.cpp and OpenAI STT as fallback. Users can record audio via ffmpeg, transcribe using local or remote STT, and generate summarized voice notes in the same markdown format as text entries. The feature includes a separate `tom record` command for storing raw recordings and transcriptions for future reprocessing.

**Key Components**:
- Cross-platform audio recording with FFmpeg (macOS, Linux, Windows)
- Local transcription via whisper.cpp CLI (privacy-focused, offline)
- Remote transcription via OpenAI API (fallback, higher accuracy)
- Automatic STT engine selection with fallback logic
- Voice notes with collapsible transcripts and LLM summaries
- Recording storage in `recording/` subdirectory for future processing

## Spec Clarifications (Session 2025-10-20)

The following clarifications were added to the spec after initial planning:

1. **Legal/Compliance**: Single-user personal device assumption (no consent UI required). Legal guidance will be documented in README and Homebrew caveats.
2. **Storage Management**: Manual cleanup only - no automatic storage quotas or retention policies. Documentation will provide storage growth guidance (~4.7 MB per 5-minute recording).
3. **Observability**: Standard operational logging (STT engine, model, processing duration, word count) using structured Serilog. No audio content or transcripts logged for privacy.
4. **Performance Targets**: Local transcription ≤ 2x realtime; OpenAI ≤ 5s overhead beyond network transfer.
5. **Accessibility**: Standard CLI output compatible with OS accessibility tools and screen readers. No custom audio feedback or special modes.

**Impact on Implementation**:
- FR-061-065 added (legal documentation, observability requirements)
- NFR-001-013 added (performance, reliability, usability, accessibility)
- SC-019-020 added (storage and logging documentation validation)

## Technical Context

**Language/Version**: C# 12 with .NET 9  
**Primary Dependencies**:
- FFmpeg (external, cross-platform audio recording)
- whisper.cpp (external, optional, local STT)
- Azure.AI.OpenAI 2.0.0 (NuGet, OpenAI STT)
- System.CommandLine 2.0.0-beta4 (existing)
- Serilog (existing)

**Storage**: File system (markdown with YAML frontmatter, WAV audio files, TXT transcripts)  
**Testing**: xUnit, FluentAssertions, Moq (existing test infrastructure)  
**Target Platform**: macOS, Linux, Windows (cross-platform CLI)  
**Project Type**: Single-project CLI application with Vertical Slice Architecture  
**Performance Goals**:
- Audio recording: Realtime (16kHz mono WAV)
- Local transcription: ≤ 2x realtime (e.g., 10 minutes to transcribe 5-minute recording)
- Remote transcription: ≤ 5 seconds overhead beyond network transfer time
- Entry generation: < 5s for LLM summarization (existing performance)

**Constraints**:
- Audio must be 16kHz, mono, PCM s16le WAV for whisper.cpp compatibility
- Minimum recording duration: 0.5 seconds
- Maximum file size for OpenAI STT: 25 MB
- whisper.cpp model file is user-provided (not bundled)

**Scale/Scope**:
- Target: Personal note use (1-10 entries per day)
- Recording length: Typically 2-5 minutes per entry
- Storage: ~4.7 MB per 5-minute recording (16kHz mono WAV)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Principle I: Modern .NET & Idiomatic C# ✅ PASS

- Using C# 12 features (primary constructors, required properties, collection expressions)
- .NET 9 target framework
- Async/await patterns for I/O operations (file system, process execution, API calls)
- Nullable reference types enabled
- Serilog for structured logging (organizational standard)

**Verdict**: Fully compliant

---

### Principle II: CLI-First Interface ✅ PASS

- No web or GUI dependencies
- Pure command-line interaction via System.CommandLine
- Standard input/output streams (stdin for stopping recording, stdout for results)
- Clear, actionable error messages with installation instructions
- Supports scripting via `--json` flag for `tom record` command

**Verdict**: Fully compliant

---

### Principle III: Test-First (NON-NEGOTIABLE) ✅ PASS

- TDD approach required: tests before implementation
- Unit tests for handlers, services, command handlers
- Integration tests for FFmpeg recording, whisper.cpp transcription (conditional execution)
- Test coverage target: 80% minimum
- Tests run conditionally based on external dependency availability

**Test Structure**:
```
tests/TenSecondTom.Tests/Features/Audio/
tests/TenSecondTom.IntegrationTests/Features/Audio/
```

**Verdict**: Fully compliant - Test-first approach will be followed

---

### Principle IV: DRY & Design Patterns ✅ PASS

**Patterns Applied**:
1. **Vertical Slice Architecture**: New `Audio` feature slice (self-contained)
2. **Command Pattern**: RecordAudioCommand, TranscribeAudioCommand, CreateVoiceNoteEntryCommand
3. **Factory Pattern**: SttProviderFactory for engine selection
4. **Strategy Pattern**: ISttProvider interface with LocalWhisperSttProvider and OpenAiSttProvider
5. **Repository Pattern**: Reuse existing FileSystemStorageProvider

**DRY Compliance**:
- Reuse existing note entry creation logic (CreateDailyEntryHandler)
- Reuse existing LLM summarization pipeline
- Reuse existing configuration infrastructure (User Secrets)
- Reuse existing storage patterns (markdown + frontmatter)

**Verdict**: Fully compliant - Follows established patterns, no duplication

---

### Principle V: Semantic Versioning & Automated Releases ✅ PASS

**Version Impact**: MINOR version bump (new feature, backward compatible)
- Existing entries continue to work (backward compatible)
- New optional CLI flags (`--voice`, `--stt`)
- New command (`tom record`)
- No breaking changes to existing commands

**Release Automation**: GitHub Actions will handle release on merge to main

**Verdict**: Fully compliant

---

### Principle VI: Cross-Platform Distribution ✅ PASS

**Platform Support**:
- macOS: FFmpeg with AVFoundation (`:0` device)
- Linux: FFmpeg with ALSA (`default` device)
- Windows: FFmpeg with DirectShow (`audio="Microphone"` device)

**Package Manager Distribution**:
- Homebrew (macOS): Add `depends_on "ffmpeg"` + optional `whisper-cpp`
- Future: Chocolatey/winget (Windows)

**Dependency Management**:
- FFmpeg: Required dependency in Homebrew formula
- whisper.cpp: Optional dependency with clear installation instructions
- Whisper model: User-provided (documented in caveats)

**Verdict**: Fully compliant - Cross-platform with proper dependency declaration

---

### Principle VII: Local Development Excellence ✅ PASS

**Developer Experience**:
- Clear README updates with voice feature setup
- Quickstart guide with platform-specific instructions
- Example usage and troubleshooting
- External dependencies clearly documented
- Local testing possible with or without whisper.cpp

**Development Setup**:
1. Clone repo
2. `dotnet restore`
3. `dotnet build`
4. Install ffmpeg (required)
5. Optional: Install whisper.cpp + model for local testing

**Verdict**: Fully compliant

---

### Principle VIII: Secrets Management ✅ PASS

**Secrets Handling**:
- No new secrets required beyond existing OpenAI API key
- whisper.cpp model path stored in User Secrets (not sensitive, but user-specific)
- No secrets in source control
- Configuration via environment variables or User Secrets

**Configuration Pattern**:
```json
{
  "Audio:LocalWhisper:ModelPath": "~/.models/ggml-base.en.bin",
  "Audio:PreferredStt": "auto",
  "Audio:Timeouts": {
    "TodaySeconds": 180,
    "RecordSeconds": 900
  }
}
```

**Verdict**: Fully compliant

---

### Overall Constitution Check Result: ✅ PASS

All 8 core principles satisfied. No violations or complexity trade-offs required. Feature integrates seamlessly with existing architecture and patterns.

## Project Structure

### Documentation (this feature)

```
specs/008-add-voice-entry/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (COMPLETE)
├── data-model.md        # Phase 1 output (COMPLETE)
├── quickstart.md        # Phase 1 output (COMPLETE)
├── contracts/           # Phase 1 output (COMPLETE)
│   ├── RecordAudioCommand.cs
│   ├── TranscribeAudioCommand.cs
│   ├── CreateVoiceNoteEntryCommand.cs
│   ├── RecordCommand.cs
│   ├── IAudioRecorder.cs
│   ├── ISttProvider.cs
│   └── ISttProviderFactory.cs
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT YET CREATED)
```

### Source Code (repository root)

Following Vertical Slice Architecture as defined in `.specify/memory/constitution.md`:

```
src/Features/Audio/
├── Commands/
│   ├── RecordAudioCommand.cs              # Start/stop audio recording
│   ├── TranscribeAudioCommand.cs          # Transcribe audio file
│   └── RecordCommand.cs                   # Record + transcribe + store
├── Handlers/
│   ├── RecordAudioCommandHandler.cs       # Orchestrates FFmpeg recording
│   ├── TranscribeAudioCommandHandler.cs   # Orchestrates STT provider selection + transcription
│   └── RecordCommandHandler.cs            # Orchestrates record + transcribe + save to recording/
├── Services/
│   ├── IAudioRecorder.cs                  # Audio recorder interface
│   ├── FfmpegAudioRecorder.cs             # FFmpeg implementation (cross-platform)
│   ├── ISttProvider.cs                    # Speech-to-text provider interface
│   ├── LocalWhisperSttProvider.cs         # whisper.cpp CLI implementation
│   ├── OpenAiSttProvider.cs               # OpenAI API implementation
│   ├── ISttProviderFactory.cs             # STT provider factory interface
│   └── SttProviderFactory.cs              # Factory for auto/local/openai selection
├── Models/
│   ├── AudioRecording.cs                  # Recording metadata
│   ├── TranscriptionResult.cs             # Transcription result
│   ├── StoredRecording.cs                 # Stored recording in recording/ dir
│   ├── AudioFormat.cs                     # Enum: Wav, Mp3, M4a
│   ├── SttEngine.cs                       # Enum: Local, OpenAI
│   └── SttSelection.cs                    # Enum: Auto, Local, OpenAI
└── DependencyInjection.cs                 # AddFeatureAudioServices()

src/Features/Today/
├── Commands/
│   ├── CreateDailyEntryCommand.cs         # EXISTING (no changes)
│   └── CreateVoiceNoteEntryCommand.cs     # NEW: Voice-specific note entry
└── Handlers/
    ├── CreateDailyEntryHandler.cs         # EXISTING (no changes needed)
    └── CreateVoiceNoteEntryHandler.cs     # NEW: Handle VoiceNoteEntry creation

src/Features/Today/Models/
└── VoiceNoteEntry.cs                      # NEW: Extends DailyEntry

src/Infrastructure/Configuration/
└── AudioConfiguration.cs                  # NEW: Audio config model

src/Infrastructure/Cli/
└── CommandRegistry.cs                     # MODIFY: Add --voice flag to today, add record command

tests/TenSecondTom.Tests/Features/Audio/
├── Services/
│   ├── FfmpegAudioRecorderTests.cs        # Unit tests (mocked Process)
│   ├── LocalWhisperSttProviderTests.cs    # Unit tests (mocked Process)
│   ├── OpenAiSttProviderTests.cs          # Unit tests (mocked API client)
│   └── SttProviderFactoryTests.cs         # Unit tests
└── Handlers/
    ├── RecordAudioCommandHandlerTests.cs
    ├── TranscribeAudioCommandHandlerTests.cs
    └── RecordCommandHandlerTests.cs

tests/TenSecondTom.IntegrationTests/Features/Audio/
├── FfmpegRecordingIntegrationTests.cs     # Requires ffmpeg (conditional)
├── WhisperTranscriptionIntegrationTests.cs # Requires whisper.cpp + model (conditional)
├── OpenAiSttIntegrationTests.cs           # Requires API key (conditional)
└── VoiceNoteEntryIntegrationTests.cs      # End-to-end voice entry workflow
```

**Structure Decision**: 

This feature follows VSA principles as defined in the constitution:

1. **Self-Contained Slice**: New `Audio` feature contains all audio recording and transcription logic
2. **Commands vs Queries**: All operations are commands (mutations) - no queries needed
3. **Handler Collocation**: Handlers in same feature folder as commands
4. **DI Registration**: `AddFeatureAudioServices()` in `Audio/DependencyInjection.cs`
5. **No Cross-Feature Dependencies**: `Audio` feature is independent; `Today` feature consumes it via interfaces
6. **Infrastructure Separation**: Audio config in `Infrastructure/Configuration`
7. **Test Mirroring**: Test structure mirrors source structure

The `Today` feature is extended (not modified) with a new command for Voice Note entries that composes the `Audio` feature. This maintains separation of concerns while enabling feature composition.

## Complexity Tracking

*No violations or complexity trade-offs identified.*

This feature integrates cleanly with existing architecture:
- No new project required (single CLI project)
- No repository pattern overhead (reuse existing storage)
- No additional dependencies beyond one NuGet package (Azure.AI.OpenAI)
- External tools (ffmpeg, whisper.cpp) are industry-standard, widely available
- Complexity is managed through standard patterns (Factory, Strategy, Command)

---

## Implementation Phases

### Phase 0: Research ✅ COMPLETE

**Status**: Complete  
**Output**: `research.md`  
**Key Decisions**:
- FFmpeg for cross-platform audio recording (mature, available on all platforms)
- whisper.cpp CLI invocation (simpler than C API binding, testable independently)
- Azure.AI.OpenAI SDK for remote transcription (official, type-safe, async)
- User Secrets for configuration (consistent with existing patterns)
- Vertical Slice Architecture with new `Audio` feature (follows constitution)
- Optional silence removal with FFmpeg (future enhancement, disabled for MVP)

---

### Phase 1: Design & Contracts ✅ COMPLETE

**Status**: Complete  
**Outputs**:
- `data-model.md` ✅
- `contracts/` ✅
- `quickstart.md` ✅

**Entities Defined**:
- AudioRecording (recording metadata)
- TranscriptionResult (STT output)
- VoiceNoteEntry (extends DailyEntry)
- StoredRecording (archived in recording/ dir)

**Contracts Defined**:
- RecordAudioCommand / RecordAudioCommandHandler
- TranscribeAudioCommand / TranscribeAudioCommandHandler
- CreateVoiceNoteEntryCommand / CreateVoiceNoteEntryHandler
- RecordCommand / RecordCommandHandler
- IAudioRecorder (FFmpeg implementation)
- ISttProvider (whisper.cpp and OpenAI implementations)
- ISttProviderFactory (auto/local/openai selection)

---

### Phase 2: Task Breakdown (Next Step)

**Command**: `/speckit.tasks`  
**Output**: `tasks.md`  
**Scope**: Break down implementation into test-first tasks following TDD

---

### Phase 3: Implementation (After Phase 2)

**Sequence**:
1. Write unit tests
2. Implement to pass tests
3. Write integration tests
4. Implement integration
5. Manual testing on each platform (macOS, Linux, Windows)

---

## Testing Strategy

### Unit Tests (Fast, No External Dependencies)

**Coverage**:
- Command validation
- Handler orchestration logic
- Factory selection logic
- Configuration loading
- Error handling paths

**Mocking**:
- Process execution (FFmpeg, whisper.cpp)
- OpenAI API client
- File system operations
- Configuration providers

**Target**: 80% code coverage minimum

---

### Integration Tests (Conditional Execution)

**Conditional on FFmpeg**:
```csharp
[Fact]
public async Task RecordAudio_WithFFmpeg_CreatesValidWavFile()
{
    if (!FfmpegAvailabilityChecker.IsAvailable())
    {
        _output.WriteLine("Skipping: ffmpeg not found on PATH");
        return;
    }
    
    // Test implementation...
}
```

**Conditional on whisper.cpp + Model**:
```csharp
[Fact]
public async Task TranscribeAudio_WithWhisperCpp_ReturnsTranscript()
{
    if (!WhisperAvailabilityChecker.IsAvailable())
    {
        _output.WriteLine("Skipping: whisper.cpp not configured");
        return;
    }
    
    // Test implementation...
}
```

**Conditional on OpenAI API Key**:
```csharp
[Fact]
public async Task TranscribeAudio_WithOpenAI_ReturnsTranscript()
{
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
    {
        _output.WriteLine("Skipping: OPENAI_API_KEY not set");
        return;
    }
    
    // Test implementation...
}
```

**CI/CD**: Integration tests may be skipped in CI if dependencies unavailable

---

### Manual Testing Checklist

**Per Platform** (macOS, Linux, Windows):
- [ ] `tom today --voice` with auto STT selection (verify timeout prompt and continuation)
- [ ] `tom today --voice --stt=local` (if whisper.cpp available)
- [ ] `tom today --voice --stt=openai` (if API key configured)
- [ ] `tom record` with default settings (verify timeout prompt and continuation)
- [ ] `tom record --json` with JSON output
- [ ] Audio file saved correctly (playable)
- [ ] Transcription accuracy acceptable
- [ ] Search returns transcript matches with snippet context
- [ ] Markdown entry formatted correctly
- [ ] Error messages clear and actionable
- [ ] Setup wizard handles audio configuration

---

## Risk Assessment

### External Dependencies

**Risk**: FFmpeg or whisper.cpp not available  
**Mitigation**:
- Clear installation instructions per platform
- Homebrew auto-installs ffmpeg
- Fallback to OpenAI STT if local unavailable
- Error messages include installation commands

### Audio Quality

**Risk**: Poor transcription due to background noise, accents, etc.  
**Mitigation**:
- Document recording best practices in quickstart
- Provide configurable STT provider selection
- Allow reprocessing in future (architecture supports it)

### Model Download

**Risk**: Users confused about downloading Whisper models  
**Mitigation**:
- Homebrew caveats provide download commands
- Setup wizard can offer to download model (future)
- Clear error messages with download links

### Cross-Platform Behavior

**Risk**: Platform-specific audio device issues  
**Mitigation**:
- Use FFmpeg's platform-specific drivers (tested on all platforms)
- Integration tests on all platforms
- Conditional test execution allows local-only testing

---

## Dependencies

### New NuGet Packages

```xml
<PackageReference Include="Azure.AI.OpenAI" Version="2.0.0" />
```

### External Tools (Not Bundled)

| Tool | Required | Purpose | Installation |
|------|----------|---------|--------------|
| FFmpeg | Yes | Audio recording | `brew install ffmpeg` |
| whisper.cpp | No | Local transcription | `brew install whisper-cpp` |
| Whisper GGML model | No | Local transcription | Download from Hugging Face |

---

## Configuration Schema

### User Secrets / Environment Variables

```json
{
  "Audio": {
    "PreferredStt": "auto",
    "Recorder": {
      "FfmpegPath": "ffmpeg"
    },
    "LocalWhisper": {
      "BinaryPath": "whisper-cpp",
      "ModelPath": "/Users/chris/.models/ggml-base.en.bin"
    },
    "OpenAI": {
      "Model": "whisper-1"
    },
    "KeepFiles": true,
    "Timeouts": {
      "TodaySeconds": 180,
      "RecordSeconds": 900
    },
    "Preprocessing": {
      "RemoveSilence": false,
      "SilenceThresholdDb": -40,
      "MinimumSilenceDurationMs": 500
    }
  }
}
```

---

## Success Criteria

### Functional

- ✅ Users can create Voice Note entries on macOS, Linux, Windows
- ✅ Local transcription works when whisper.cpp + model available
- ✅ OpenAI fallback works when local unavailable (with auto selection)
- ✅ Explicit STT selection works (local/openai flags)
- ✅ `tom record` command stores audio + transcript in recording/ directory
- ✅ Voice entries display with audio metadata, transcript, and summary
- ✅ Error messages are clear and actionable

### Non-Functional

- ✅ 80% test coverage (unit + integration)
- ✅ No compiler warnings
- ✅ Follows constitution principles (all 8)
- ✅ Documentation complete (quickstart, research, data model)
- ✅ Cross-platform compatibility verified
- ✅ Performance targets met (≤ 2x realtime local, ≤ 5s OpenAI overhead)
- ✅ Operational logging includes STT metrics (engine, model, duration, word count)
- ✅ Standard CLI output compatible with screen readers (no color-only status)

### Documentation

- ✅ Legal guidance documented (single-user personal device assumption, recording consent laws)
- ✅ Storage management guidance provided (~4.7 MB per 5-minute recording, manual cleanup procedures)
- ✅ Homebrew caveats include legal and model download information
- ✅ README includes accessibility notes (OS-level tools, screen reader compatibility)

---

## Next Steps

1. **Generate task breakdown**: Run `/speckit.tasks` command
2. **Implement tests first**: Follow TDD discipline (red-green-refactor)
3. **Implement feature**: Complete all tasks in tasks.md
4. **Manual testing**: Test on all platforms (macOS, Linux, Windows)
5. **Update documentation**: README, CHANGELOG, release notes
6. **Merge to main**: Trigger automated release

---

**Plan Version**: 1.0.0  
**Last Updated**: 2025-10-20  
**Research Status**: Complete ✅  
**Design Status**: Complete ✅  
**Implementation Status**: Not Started ⏸️
