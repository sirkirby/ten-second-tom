# Feature Specification: Voice Entry with Local-First Speech-to-Text

**Feature Branch**: `008-add-voice-entry`  
**Created**: 2025-10-20  
**Status**: Draft  
**Input**: User description: "Add voice entry with local-first speech-to-text (STT) using whisper.cpp, with OpenAI STT as an option and fallback. Audio is recorded locally via ffmpeg, transcribed per the selected engine, then summarized using the existing OpenAI summary path, and written as a Markdown memory exactly like current text entries."

## Clarifications

### Session 2025-10-20

- Q: Audio recording involves legal considerations around consent and data protection (especially in two-party consent jurisdictions). How should the system handle recording consent and privacy compliance? → A: Single-user personal device (no consent UI, document assumptions in README/caveats)
- Q: Audio files can accumulate quickly (a 5-minute recording = ~4.7 MB). Should the system implement automatic cleanup or storage limits for audio files? → A: Manual cleanup only (user responsibility, document storage growth in README)
- Q: Beyond error logging, what operational metrics or logs should the system track for voice transcription operations (useful for debugging transcription quality or performance issues)? → A: Standard operational logs (engine used, processing duration, word count, model used)
- Q: What are acceptable latency thresholds for the transcription workflow? This helps validate whether performance optimizations (like silence removal) are needed. → A: 2x realtime for local, 5s overhead for OpenAI
- Q: For CLI voice interaction, should the system provide any special considerations for users with visual or hearing impairments (e.g., audio feedback, screen reader compatibility)? → A: Standard CLI output (rely on OS accessibility tools and screen readers)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Quick Voice Note with Auto STT Selection (Priority: P1)

A user wants to quickly record their thoughts by voice instead of typing, without worrying about technical setup. The system automatically chooses the best available transcription method (local-first, falling back to OpenAI if needed).

**Why this priority**: This is the core value proposition - enabling voice input with minimal friction. Users shouldn't need to understand the technical details of STT engines.

**Independent Test**: Can be fully tested by running `tom today --voice`, recording audio, and verifying a markdown note is created with transcript and summary sections. Recording auto-stops after a configurable timeout unless the user presses a key to continue.

**Acceptance Scenarios**:

1. **Given** the user has whisper.cpp installed and configured, **When** they run `tom today --voice` and record audio, **Then** the system transcribes locally and creates a markdown note with audio metadata, collapsible transcript, and summary; recording respects the configured timeout with a "press any key to continue or Enter to stop" UX
2. **Given** the user does NOT have whisper.cpp installed, **When** they run `tom today --voice`, **Then** the system automatically falls back to OpenAI STT and creates the entry successfully
3. **Given** the user is running the voice command for the first time with no STT preference set, **When** they run `tom today --voice`, **Then** the system prompts them once to choose "local or openai" preference and persists their choice for future use

---

### User Story 2 - Explicit STT Engine Selection (Priority: P2)

A user wants full control over which transcription engine is used, either to force local-only processing for privacy, or to explicitly use OpenAI for better accuracy.

**Why this priority**: Power users need control over transcription method for privacy/compliance reasons or quality preferences. This enables users in secure environments or those with specific quality needs.

**Independent Test**: Can be tested independently by running `tom today --voice --stt=local` and `tom today --voice --stt=openai` and verifying the correct engine is used (observable through logs or error messages when local engine is unavailable).

**Acceptance Scenarios**:

1. **Given** the user has whisper.cpp installed, **When** they run `tom today --voice --stt=local`, **Then** the system uses local transcription only and never attempts OpenAI fallback
2. **Given** the user does NOT have whisper.cpp installed, **When** they run `tom today --voice --stt=local`, **Then** the system fails with a clear error message explaining whisper.cpp is unavailable and suggesting configuration steps
3. **Given** the user wants to use OpenAI exclusively, **When** they run `tom today --voice --stt=openai`, **Then** the system uses OpenAI STT only, regardless of local whisper.cpp availability

---

### User Story 3 - Record and Store Audio with Transcription (Priority: P3)

A user wants to capture and store audio recordings with transcriptions for future processing - perhaps for meeting notes, interview transcripts, or raw material that can later be processed with different prompts and commands.

**Why this priority**: This provides a foundation for building a library of audio content that can be reprocessed, summarized differently, or transcribed with improved models later. It enables workflows beyond immediate note entries and sets up future capabilities like batch transcription and prompt-based processing of stored audio.

**Independent Test**: Can be tested independently by running `tom record`, verifying audio and transcription files are saved to the configured memory directory under `recording/` subdirectory, and checking the file structure matches other commands like `today` and `thisweek`. Recording stops at a configurable timeout unless the user confirms continuation.

**Acceptance Scenarios**:

1. **Given** the user wants to capture audio for later processing, **When** they run `tom record`, **Then** the system records audio, transcribes it using the configured STT engine, stores both the raw audio file and transcription text in the `recording/` subdirectory of the configured memory directory, and displays confirmation with file paths
2. **Given** the user wants structured output, **When** they run `tom record --json`, **Then** the system outputs JSON to stdout in the format `{"audio_path": "...", "transcription_path": "...", "text": "...", "duration_seconds": 42}` after saving files
3. **Given** the user has stored recordings, **When** they later run a future `tom transcribe` command, **Then** the system can reprocess stored audio files with different transcription providers or settings
4. **Given** the user has stored transcriptions, **When** they want to process them with existing command prompts, **Then** the system can apply `today` or other command prompts to the stored transcription text to generate different summaries or outputs

---

### User Story 4 - Review Voice Notes (Priority: P2)

A user wants to review their voice-recorded note entries in the same way they review text entries, with clear indication of which entries came from voice vs. text.

**Why this priority**: Viewing and searching entries is a core feature. Voice entries must integrate seamlessly with existing entry review workflows.

**Independent Test**: Can be tested by creating both text and voice entries, then running existing review commands (e.g., `tom search`) and verifying voice entries are properly displayed with audio metadata. Search returns matches from transcript text with a short snippet around the term.

**Acceptance Scenarios**:

1. **Given** the user has created voice notes, **When** they view entries using existing review commands, **Then** voice notes display with audio filename, duration, collapsible transcript section, and summary formatted identically to text entries
2. **Given** the user searches entries, **When** search terms match transcript or summary content from voice entries, **Then** those voice entries appear in search results with a snippet showing 50 characters before and after the match term
3. **Given** the user wants to access original audio, **When** they view a voice entry, **Then** the markdown clearly shows the audio filename and path where the original recording is stored

---

### Edge Cases

- What happens when the user stops recording after only 1 second of audio? System should handle very short recordings gracefully without crashing
- What happens when whisper.cpp is installed but the model file path is not configured? System should fail with clear error message directing user to configure `Audio:LocalWhisper:ModelPath`
- What happens when OpenAI API rate limits are hit during fallback? System should display rate limit error with retry suggestion
- What happens when disk space is insufficient to save the audio file? System should detect and fail gracefully before recording starts
- What happens when ffmpeg is not installed or not found on PATH? System should fail immediately with installation instructions specific to the user's OS
- What happens when the user interrupts recording (Ctrl+C) before pressing Enter? System should clean up the partial recording file
- What happens when the audio file is corrupted or unreadable by the transcriber? System should detect and report with a helpful error message
- What happens when `Audio:KeepFiles` is false for note entries? System should delete the audio file after successful transcription and entry creation (note: `tom record` command always persists files regardless of this setting)
- What happens when the user has whisper.cpp but an incompatible model format? System should detect and fail with model format requirements
- What happens when OpenAI STT returns an empty transcript? System should handle gracefully and either prompt user to re-record or save an entry with a note about empty transcript
- What happens when the configured memory directory is not writable? System should detect permission issues and fail with clear error before recording
- What happens when an audio recording contains long periods of silence? Currently transcribed as-is; future preprocessing feature (FR-016-019) will optimize this
- What happens when the user runs `tom record` but the `recording/` subdirectory doesn't exist yet? System should create it automatically following the same pattern as other command directories

## Requirements *(mandatory)*

### Functional Requirements

#### CLI Interface

- **FR-001**: System MUST extend the `tom today` command with a `--voice` flag to enable Voice Note entry mode
- **FR-002**: System MUST provide a `--stt=auto|local|openai` option with default value `auto` to control STT engine selection
- **FR-003**: System MUST provide a new command `tom record` that records audio, transcribes it, and stores both raw audio and transcription in the configured memory directory under a `recording/` subdirectory
- **FR-004**: The `tom record` command MUST support a `--json` flag to output recording metadata and transcription results as JSON to stdout (in addition to saving files)
- **FR-005**: The `tom record` command MUST follow the same storage directory structure pattern as other commands (`today`, `thisweek`) within the configured memory directory
- **FR-006**: System MUST display clear usage help for voice-related commands including examples
- **FR-006a**: System MUST support a configurable recording timeout for `today --voice` and `record`, with per-command defaults (180s for today, 900s for record)
- **FR-006b**: On timeout, system MUST display a non-blocking prompt "Recording timeout reached. Press any key to continue recording, or press Enter to stop." and wait up to 10 seconds for user input using async console input polling; if no input within 10 seconds, automatically stop and finalize the recording

#### Audio Recording

- **FR-007**: System MUST use ffmpeg as the cross-platform audio recording mechanism
- **FR-008**: System MUST record audio in WAV format with specific settings: 16 kHz sample rate, mono channel, PCM s16le encoding
- **FR-009**: For `tom today --voice`, system MUST save recorded audio files to the configured memory directory with naming pattern `note-YYYYMMdd-HHmmss.wav` (storage location controlled by `Audio:KeepFiles` setting)
- **FR-010**: For `tom record`, system MUST save recorded audio files to `<memory-dir>/recording/recording-YYYYMMdd-HHmmss.wav` and transcription to `<memory-dir>/recording/recording-YYYYMMdd-HHmmss.txt` (always persisted regardless of `Audio:KeepFiles` setting)
- **FR-011**: System MUST display a simple UX during recording: "Recording… press Enter to stop."
- **FR-012**: System MUST terminate the ffmpeg process when the user presses Enter to properly finalize WAV headers
- **FR-013**: System MUST support OS-specific audio device inputs for macOS, Linux, and Windows
- **FR-014**: System MUST check for ffmpeg availability before attempting to record and fail with installation instructions if not found
- **FR-015**: System MUST automatically create the `recording/` subdirectory in the configured memory directory if it does not exist

#### Audio Preprocessing and Optimization (Future Enhancement - Out of MVP Scope)

**Note**: Audio preprocessing features (FR-016 through FR-019) are planned for future releases and are NOT part of the MVP implementation (US1-US4). Configuration keys are defined for forward compatibility but will have no effect until preprocessing is implemented.

- **FR-016**: System SHOULD implement audio preprocessing to optimize transcription efficiency and cost (Future)
- **FR-017**: System SHOULD detect and remove or compress long periods of silence in audio recordings before transcription (Future)
- **FR-018**: System SHOULD provide configurable thresholds for silence detection (e.g., minimum silence duration, silence threshold in dB) (Future)
- **FR-019**: System MAY implement audio compression techniques that preserve speech quality while reducing file size for remote transcription services (Future)

#### Speech-to-Text Provider Requirements (Common)

All STT provider implementations MUST:

- Check for provider availability before attempting transcription (verify configuration, binaries, API keys, etc.)
- Handle errors with clear, actionable error messages including specific remediation steps
- Return structured transcription results with metadata (engine type, model identifier, processing duration, word count)
- Support cancellation via CancellationToken
- Log transcription operations with structured logging (engine, model, duration, word count) without logging transcript content

#### Local Speech-to-Text (whisper.cpp)

- **FR-020**: System MUST implement a local transcription path using the whisper.cpp CLI binary
- **FR-021**: System MUST invoke whisper.cpp with arguments: `-m <model.bin> -f <wav> -otxt -of <tmp-prefix>`
- **FR-022**: System MUST read the transcription output from `<tmp-prefix>.txt` after whisper.cpp completes
- **FR-023**: System MUST handle non-zero exit codes from whisper.cpp and report errors with installation/configuration guidance
- **FR-024**: System MUST require users to provide their own GGML model file and configure the path via `Audio:LocalWhisper:ModelPath`
- **FR-025**: System MUST check for whisper.cpp binary and model file availability before attempting transcription

#### OpenAI Speech-to-Text

- **FR-026**: System MUST implement OpenAI STT using the official OpenAI .NET SDK
- **FR-027**: System MUST default to the `whisper-1` model for OpenAI transcriptions
- **FR-028**: System MUST allow users to configure an alternative OpenAI model via `Audio:OpenAI:Model` (for future model versions like `whisper-2` when available)
- **FR-029**: System MUST handle OpenAI API errors (network, authentication, rate limits) with retry suggestions and configuration guidance
- **FR-030**: System MUST support both text and JSON response formats from OpenAI STT API

#### STT Engine Selection Strategy

- **FR-031**: When `--stt=auto` (default), system MUST attempt local transcription first and fall back to OpenAI on local engine failure
- **FR-032**: When `--stt=local`, system MUST use whisper.cpp only and fail with clear error if unavailable
- **FR-033**: When `--stt=openai`, system MUST use OpenAI STT only regardless of local engine availability
- **FR-034**: System MUST log which STT engine is being used for each transcription attempt
- **FR-035**: System MUST distinguish between "local engine not available" (trigger fallback) and "local engine failed during transcription" (report error)

#### Observability and Logging

- **FR-062**: System MUST log standard operational metrics for each transcription: STT engine used, model identifier, processing duration, word count, and audio duration
- **FR-063**: System MUST use structured logging (Serilog) with semantic properties for easy filtering and analysis
- **FR-064**: System MUST log at Information level for successful operations and Error level for failures
- **FR-065**: System MUST NOT log audio content or transcription text in logs (privacy protection)

#### Configuration Management

- **FR-036**: System MUST support `Audio:PreferredStt` configuration value: `auto|local|openai` (default: `auto`)
- **FR-037**: System MUST support `Audio:Recorder:FfmpegPath` configuration (default: `ffmpeg` on PATH)
- **FR-038**: System MUST support `Audio:LocalWhisper:BinaryPath` configuration (default: `whisper-cpp` on PATH)
- **FR-039**: System MUST support `Audio:LocalWhisper:ModelPath` configuration (user must provide, no default)
- **FR-040**: System MUST support `Audio:OpenAI:Model` configuration (default: `whisper-1`)
- **FR-041**: System MUST support `Audio:KeepFiles` boolean configuration (default: `true`) to control whether audio files are retained after transcription for note entries (note: `tom record` always persists files)
- **FR-042**: System MUST define `Audio:Preprocessing:RemoveSilence` boolean configuration key for future use (currently has no effect)
- **FR-043**: System MUST define `Audio:Preprocessing:SilenceThresholdDb` configuration key (range: -60 to 0 dB) for future use (currently has no effect)
- **FR-044**: System MUST define `Audio:Preprocessing:MinimumSilenceDurationMs` configuration key (minimum: 100ms) for future use (currently has no effect)
- **FR-044a**: System MUST support `Audio:Timeouts:TodaySeconds` (default: 180) and `Audio:Timeouts:RecordSeconds` (default: 900) configuration values controlling per-command recording timeouts
- **FR-045**: On first run when `Audio:PreferredStt` is not configured, system MUST prompt user once: "Prefer local or OpenAI transcription?" and persist their choice
- **FR-046**: System MUST respect existing configuration patterns (environment variables, user secrets, appsettings.json)

#### Markdown Entry Generation

- **FR-047**: System MUST create markdown notes from voice recordings with the same structure as existing text entries
- **FR-048**: Voice entry metadata section MUST include the audio filename and duration in seconds
- **FR-049**: Voice entries MUST include a collapsible `<details>` section containing the full transcript with summary heading "Transcript"
- **FR-050**: Voice entries MUST include a "Summary" section with LLM-generated summary using the existing summarization pipeline
- **FR-051**: System MUST reuse existing note entry creation, storage, and formatting logic wherever possible
- **FR-052**: System MUST handle transcript and summary formatting to prevent markdown rendering issues (escape special characters, handle code blocks, etc.)

#### Storage and File Management

- **FR-053**: System MUST store `tom record` audio files in `<memory-dir>/recording/` subdirectory with naming pattern `recording-YYYYMMdd-HHmmss.wav`
- **FR-054**: System MUST store `tom record` transcriptions in `<memory-dir>/recording/` subdirectory with naming pattern `recording-YYYYMMdd-HHmmss.txt`
- **FR-055**: System MUST support future reprocessing of stored recordings via a planned `tom transcribe` command
- **FR-056**: System MUST support future processing of stored transcriptions with existing command prompts (e.g., applying `today` prompt to a stored transcription)

#### Search and Review Integration

- **FR-066**: `tom search` MUST search transcript text for voice notes in addition to summaries and titles
- **FR-067**: Search results SHOULD include a short snippet from the transcript around the match term (50 characters before and after the match)

#### Homebrew Distribution

- **FR-057**: Homebrew formula MUST add `depends_on "ffmpeg"` as a required dependency
- **FR-058**: Homebrew formula SHOULD add `depends_on "whisper-cpp"` as an optional dependency
- **FR-059**: Homebrew formula MUST include caveats instructing users to download a Whisper GGML model and configure `Audio:LocalWhisper:ModelPath`
- **FR-060**: Homebrew caveats MUST provide example commands for downloading a recommended model (e.g., `ggml-large-v3-q5_0.bin`)
- **FR-061**: Homebrew caveats and README MUST document the single-user personal device assumption and remind users recording others to comply with local recording consent laws

### Non-Functional Requirements

#### Performance

- **NFR-001**: Local transcription (whisper.cpp) SHOULD complete within 2x realtime (e.g., 10 minutes to transcribe a 5-minute recording)
- **NFR-002**: OpenAI transcription SHOULD add no more than 5 seconds of overhead beyond network transfer time
- **NFR-003**: Audio recording MUST operate at realtime (no dropped frames or audio artifacts)
- **NFR-004**: System MUST remain responsive during transcription (non-blocking UI where applicable)

#### Reliability

- **NFR-005**: System MUST handle graceful degradation when local transcription fails (fallback to OpenAI with auto selection)
- **NFR-006**: System MUST preserve partial audio recordings on crash or interruption for manual recovery
- **NFR-007**: System MUST validate audio file integrity before attempting transcription

#### Usability

- **NFR-008**: Error messages MUST be actionable with specific remediation steps (installation commands, configuration examples)
- **NFR-009**: Recording UX MUST provide clear visual feedback (recording indicator, duration counter if feasible)
- **NFR-010**: System MUST complete the full workflow (record → transcribe → summarize → save) without requiring multiple command invocations
- **NFR-014**: Timeout handling MUST be deterministic (always triggers at the configured timeout value) and not cause corrupted WAV files; WAV headers MUST be properly finalized when stopping on timeout, whether user continues or auto-stops after 10s

#### Accessibility

- **NFR-011**: System MUST use standard CLI output to stdout/stderr compatible with OS-level accessibility tools and screen readers
- **NFR-012**: System MUST NOT rely on color-only information for critical status (use symbols or text labels)
- **NFR-013**: System SHOULD provide clear textual status updates during long-running operations (e.g., "Transcribing..." with periodic progress indicators)

### Key Entities

- **AudioRecording**: Represents a captured voice recording with attributes: filename, file path, duration, sample rate, format, recording timestamp
- **TranscriptionResult**: Represents the output of STT processing with attributes: source audio reference, transcript text, STT engine used, confidence score (if available), processing duration
- **SttEngineConfiguration**: Represents configuration for a speech-to-text engine with attributes: engine type (local/OpenAI), binary path (for local), model identifier, enabled status
- **VoiceNoteEntry**: Extends DailyEntry with attributes: audio filename, audio duration, transcript text (full), summary text, STT engine used
- **StoredRecording**: Represents an archived recording in the `recording/` directory with attributes: audio file path, transcription file path, original recording timestamp, duration, file size

## Assumptions and Future Considerations

### Assumptions

- **Single-user personal device**: This feature is designed for personal note use on the user's own device, recording the user's own voice. No consent UI is required. Users in jurisdictions with specific recording consent laws or using the tool to record others are responsible for compliance. Legal assumptions and appropriate use guidance will be documented in README and Homebrew caveats.
- **Manual storage management**: The system does not implement automatic cleanup or storage quotas for audio files. Users are responsible for managing their storage space. The `Audio:KeepFiles` setting controls note entry audio retention, but `tom record` always persists files. Documentation will provide guidance on storage growth (approximately 4.7 MB per 5-minute recording) and manual cleanup procedures.
- **Audio preprocessing research**: The implementation will research and evaluate audio preprocessing techniques (silence removal, compression) during development. Specific algorithms and thresholds will be determined based on testing with real-world audio samples.
- **Storage patterns**: The `recording/` directory follows the same organizational pattern as `today/` and `thisweek/` directories, using the configured memory directory as the root.
- **Model availability**: Users are responsible for downloading and maintaining their own Whisper GGML model files for local transcription.
- **Cross-platform compatibility**: ffmpeg device names and audio input mechanisms differ by OS, but ffmpeg itself handles cross-platform abstraction.

### Future Enhancements (Out of Scope)

- **`tom transcribe` command**: A planned future command that will:
  - Reprocess stored audio files from the `recording/` directory using local or remote STT providers
  - Allow users to re-transcribe with improved models or different providers
  - Support batch processing of multiple recordings
  - The current architecture (stored audio + transcription files) is designed to support this future capability

- **Prompt-based reprocessing**: A planned future capability to:
  - Apply existing command prompts (e.g., `today` prompt template) to stored transcription files
  - Generate alternative summaries or outputs from the same raw transcript
  - Enable experimentation with different prompt templates on historical content
  - The separation of transcription storage and note entry generation enables this workflow

- **Device selection**: A future `--voice-device` option to allow users to explicitly select audio input devices, with device enumeration help per OS.

- **Advanced audio preprocessing**: Future enhancements may include:
  - Noise reduction and audio enhancement
  - Speaker diarization for multi-speaker recordings
  - Automatic language detection
  - Audio quality assessment and recommendations

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can successfully record and create voice notes on macOS, Linux, and Windows without manual device configuration
- **SC-002**: When whisper.cpp is available, system successfully transcribes locally in 100% of attempts with valid audio input
- **SC-003**: When whisper.cpp is unavailable with `--stt=auto`, system successfully falls back to OpenAI STT in 100% of attempts
- **SC-004**: When `--stt=local` is used without whisper.cpp available, system fails immediately with actionable error message (no fallback)
- **SC-005**: Voice Note entries include audio metadata, collapsible transcript, and summary formatted identically to text entries
- **SC-006**: The `tom record` command successfully stores both audio and transcription files in the `recording/` subdirectory of the configured memory directory
- **SC-007**: The `tom record --json` command outputs valid JSON to stdout that can be piped to other tools, including file paths, transcription text, and duration
- **SC-008**: System provides clear error messages for all common failure scenarios (ffmpeg missing, model not configured, API errors, permission issues, etc.)
- **SC-009**: First-time users are prompted once for STT preference and their choice is persisted for subsequent uses
- **SC-010**: Audio files are recorded in WAV format at 16 kHz, mono, PCM s16le as required by whisper.cpp
- **SC-011**: When silence removal is enabled, system successfully reduces audio file size by removing long pauses while preserving speech content
- **SC-012**: Stored recordings and transcriptions from `tom record` can be located and accessed for future reprocessing
- **SC-013**: The `recording/` subdirectory is automatically created if it doesn't exist, following the same pattern as `today/` and `thisweek/`
- **SC-014**: Unit test coverage for audio/transcription features meets the project's 80% minimum threshold
- **SC-015**: Integration tests run conditionally based on availability of whisper.cpp and OpenAI API credentials
- **SC-016**: Documentation clearly explains prerequisites (ffmpeg, optional whisper.cpp), model setup, audio preprocessing options, and usage examples
- **SC-017**: Homebrew tap includes ffmpeg as dependency and warns users about model download requirements
- **SC-018**: Future `tom transcribe` command can successfully process stored audio files from the `recording/` directory (architecture supports this future enhancement)
- **SC-019**: Documentation provides clear guidance on storage growth expectations (~4.7 MB per 5-minute recording) and manual cleanup procedures for both note audio and stored recordings
- **SC-020**: System logs contain operational metrics for transcription operations (engine, model, duration, word count) in structured format, queryable via standard log analysis tools
