# Tasks — Voice Entry with Local‑First STT (Feature 008-add-voice-entry)

Branch: `008-add-voice-entry`  
Feature name: Voice Entry with Local‑First Speech‑to‑Text  
Generated: 2025-10-20

Context: Implement voice notes via FFmpeg recording, local whisper.cpp transcription with OpenAI fallback, CLI flags, storage patterns, and logging. Tasks are dependency-ordered, organized by user story, and marked [P] if parallelizable (different files).

Notes:
- Tests are required (repo constitution). Within each story, tests precede implementation (TDD).
- All paths below are absolute.

---

## Phase 1 — Setup (project initialization)

T001 [Setup] Pin Azure.AI.OpenAI NuGet in project  
- Edit: `/Users/chris/Repos/ten-second-tom/src/TenSecondTom.csproj`  
- Add package reference: `<PackageReference Include="Azure.AI.OpenAI" Version="2.0.0" />`  
- Rationale: Required for OpenAI STT provider

T002 [Setup] Create audio configuration model  
- Add: `/Users/chris/Repos/ten-second-tom/src/Infrastructure/Configuration/AudioConfiguration.cs`  
- Include nested `RecorderConfiguration`, `LocalWhisperConfiguration`, `OpenAiSttConfiguration`, `RecordingTimeoutsConfiguration` (with `TodaySeconds` default 180, `RecordSeconds` default 900), `PreprocessingConfiguration` (future use only) as per data-model.md  
- XML‑document all public members
- Note: Preprocessing config keys defined for forward compatibility but have no effect in MVP

T003 [Setup] Bind Audio configuration in DI  
- Edit: `/Users/chris/Repos/ten-second-tom/src/Infrastructure/DependencyInjection/DependencyInjection.cs`  
- Bind `AudioConfiguration` from `Configuration.GetSection("Audio")`  
- Expose strongly-typed options via DI

T004 [Setup] Add Audio config keys to appsettings and example  
- Edit: `/Users/chris/Repos/ten-second-tom/src/appsettings.json`  
- Edit: `/Users/chris/Repos/ten-second-tom/src/appsettings.Development.json`  
- Edit: `/Users/chris/Repos/ten-second-tom/example.appsettings.json`  
- Add keys with safe defaults (match plan/spec)

T005 [Setup] Create feature folder structure  
- Create directories:  
  - `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Commands/`  
  - `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Handlers/`  
  - `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Models/`  
  - `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Services/`  
  - `/Users/chris/Repos/ten-second-tom/src/Features/Audio/` (root for `DependencyInjection.cs`)  
- Mirror test folders:  
  - `/Users/chris/Repos/ten-second-tom/tests/TenSecondTom.Tests/Features/Audio/Services/`  
  - `/Users/chris/Repos/ten-second-tom/tests/TenSecondTom.Tests/Features/Audio/Handlers/`  
  - `/Users/chris/Repos/ten-second-tom/tests/TenSecondTom.IntegrationTests/Features/Audio/`

---

## Phase 2 — Foundational (blocking prerequisites for all stories)

T006 [Foundation] Add Audio enums and core models [P]  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Models/SttEngine.cs`  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Models/SttSelection.cs`  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Models/AudioFormat.cs`  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Models/AudioRecording.cs` (properties from data-model.md)  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Models/TranscriptionResult.cs`  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Models/StoredRecording.cs`  
- Include XML docs, validation guards

T007 [Foundation] Define Audio service interfaces [P]  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Services/IAudioRecorder.cs`  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Services/ISttProvider.cs`  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Services/ISttProviderFactory.cs`  
- Signatures per contracts and data-model.md, with XML docs

T008 [Foundation] Audio DI registration stub [P]  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Audio/DependencyInjection.cs`  
- Provide `AddFeatureAudioServices(this IServiceCollection services)`; empty registrations for now  
- Edit: `/Users/chris/Repos/ten-second-tom/src/Infrastructure/DependencyInjection/DependencyInjection.cs` to call `services.AddFeatureAudioServices()`

T009 [Foundation] Prepare Today voice note types [P]  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Today/Models/VoiceNoteEntry.cs` (extends existing entry shape)  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Today/Commands/CreateVoiceNoteEntryCommand.cs` (record per contracts)  
- XML‑document public API

Checkpoint: Foundation complete. Proceed to user stories.

---

## Phase 3 — US1 (P1): Quick Voice Note with Auto STT
Story goal: `tom today --voice` records, transcribes (local‑first, OpenAI fallback), summarizes, and writes markdown note with transcript.
Independent test: Run `tom today --voice`, verify note created with audio metadata, collapsible transcript, and summary.

Tests (TDD first)

T010 [US1][Tests] Unit: STT provider factory auto selection and fallback [P]  
- Add: `/Users/chris/Repos/ten-second-tom/tests/TenSecondTom.Tests/Features/Audio/Services/SttProviderFactoryTests.cs`  
- Cases: local available→Local; local unavailable→OpenAI; failure paths logged; config `PreferredStt=auto`

T011 [US1][Tests] Unit: FFmpeg recorder process orchestration [P]  
- Add: `/Users/chris/Repos/ten-second-tom/tests/TenSecondTom.Tests/Features/Audio/Services/FfmpegAudioRecorderTests.cs`  
- Mock `Process`; verify stdin `q` exit; WAV settings (16k/mono/pcm_s16le); min duration guard
- Verify timeout path: displays "Recording timeout reached. Press any key to continue recording, or press Enter to stop." prompt using async console polling; continues on any key; stops on Enter or 10s no-input; finalizes WAV headers cleanly in all cases

T012 [US1][Tests] Unit: Local whisper.cpp provider CLI invocation [P]  
- Add: `/Users/chris/Repos/ten-second-tom/tests/TenSecondTom.Tests/Features/Audio/Services/LocalWhisperSttProviderTests.cs`  
- Args: `-m <model> -f <wav> -otxt -of <tmp>`; exit code handling; output file read

T013 [US1][Tests] Unit: OpenAI STT provider API invocation [P]  
- Add: `/Users/chris/Repos/ten-second-tom/tests/TenSecondTom.Tests/Features/Audio/Services/OpenAiSttProviderTests.cs`  
- Mock SDK; success, 401, 429; returns text; logs errors

T014 [US1][Tests] Unit: RecordAudioCommandHandler orchestration [P]  
- Add: `/Users/chris/Repos/ten-second-tom/tests/TenSecondTom.Tests/Features/Audio/Handlers/RecordAudioCommandHandlerTests.cs`  
- Uses `IAudioRecorder`; creates `AudioRecording`; stop on Enter flow

T015 [US1][Tests] Unit: TranscribeAudioCommandHandler (auto selection) [P]  
- Add: `/Users/chris/Repos/ten-second-tom/tests/TenSecondTom.Tests/Features/Audio/Handlers/TranscribeAudioCommandHandlerTests.cs`  
- Uses factory; local then fallback; metrics captured

T016 [US1][Tests] Unit: CreateVoiceNoteEntryHandler formatting [P]  
- Add: `/Users/chris/Repos/ten-second-tom/tests/TenSecondTom.Tests/Features/Today/Handlers/CreateVoiceNoteEntryHandlerTests.cs`  
- Verifies frontmatter (audio metadata), collapsible transcript, summary reuse

T017 [US1][Tests] Integration: `tom today --voice` end‑to‑end (conditional)  
- Add: `/Users/chris/Repos/ten-second-tom/tests/TenSecondTom.IntegrationTests/Features/Audio/VoiceNoteEntryIntegrationTests.cs`  
- Skip if ffmpeg not available; skip local STT if whisper not available; assert entry created

Implementation

T018 [US1] Implement FFmpeg audio recorder [P]  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Services/FfmpegAudioRecorder.cs`  
- Cross‑platform device args; stdin 'q' termination; WAV 16k/mono/pcm_s16le; duration calc; guards
- Support configurable timeout via `Audio:Timeouts:TodaySeconds` and `RecordSeconds`; on timeout display "Recording timeout reached. Press any key to continue recording, or press Enter to stop." with async console polling; continue on any key; stop on Enter or after 10s no-input; properly finalize WAV headers in all stop scenarios

T019 [US1] Implement local whisper.cpp STT provider [P]  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Services/LocalWhisperSttProvider.cs`  
- CLI call; temp output prefix; read `.txt`; errors/exit codes; availability check

T020 [US1] Implement OpenAI STT provider [P]  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Services/OpenAiSttProvider.cs`  
- Use `Azure.AI.OpenAI` client; text format; error handling (429/401/other)

T021 [US1] Implement STT provider factory (auto selection) [P]  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Services/SttProviderFactory.cs`  
- Strategy: try local if available; fallback to OpenAI; structured logs

T022 [US1] Add Audio DI registrations  
- Edit: `/Users/chris/Repos/ten-second-tom/src/Features/Audio/DependencyInjection.cs`  
- Register `IAudioRecorder`, both `ISttProvider` impls, `ISttProviderFactory`

T023 [US1] Add commands and handlers (record/transcribe) [P]  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Commands/RecordAudioCommand.cs`  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Handlers/RecordAudioCommandHandler.cs`  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Commands/TranscribeAudioCommand.cs`  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Handlers/TranscribeAudioCommandHandler.cs`

T024 [US1] Create voice note command/handler  
- Edit(Add): `/Users/chris/Repos/ten-second-tom/src/Features/Today/Commands/CreateVoiceNoteEntryCommand.cs`  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Today/Handlers/CreateVoiceNoteEntryHandler.cs`  
- Compose existing entry generation + LLM summary; add transcript `<details>`

T025 [US1] CLI: Add `--voice` to `tom today` and wire flow  
- Edit: `/Users/chris/Repos/ten-second-tom/src/Infrastructure/Cli/CommandRegistry.cs`  
- Option `--voice`; on set: record → transcribe (auto) → create voice entry; respect `Audio:KeepFiles`
  - Pass `MaxDuration` per command default; expose optional `--max-seconds` override for today (design-only)

T026 [US1] First-run STT preference prompt (persist)  
- Edit: `/Users/chris/Repos/ten-second-tom/src/Infrastructure/Cli/CommandRegistry.cs` (or setup path)  
- When `Audio:PreferredStt` is not configured on first voice command invocation: prompt once "Prefer local or OpenAI transcription?", save choice to user secrets (FR-045)
- Add tests: persist choice and do not reprompt on subsequent runs (mock user secrets)

T027 [US1] Observability: structured logs and metrics  
- Edits: providers/handlers to log engine, model, durations, word count; Information/Error levels; no transcript contents in logs
  - Add periodic textual status for long ops (e.g., "Transcribing...")
  - Tests: assert structured log properties via test sink; assert status messages via injected progress reporter

Checkpoint: US1 complete.

---

## Phase 4 — US2 (P2): Explicit STT Engine Selection
Story goal: User can force `--stt=local|openai`; local fails fast if unavailable; openai ignores local.
Independent test: Run `tom today --voice --stt=local|openai` and verify engine behavior/logs.

Tests (TDD first)

T028 [US2][Tests] CLI parsing and precedence tests [P]  
- Add: `/Users/chris/Repos/ten-second-tom/tests/TenSecondTom.Tests/Features/Audio/Cli/TodayVoiceCliTests.cs`  
- Cases: flag overrides config; `--stt=local` with no whisper→clear error; `--stt=openai` skips local

T029 [US2][Tests] Factory respects explicit selection [P]  
- Add: `/Users/chris/Repos/ten-second-tom/tests/TenSecondTom.Tests/Features/Audio/Services/SttProviderFactoryExplicitSelectionTests.cs`  
- Ensure no fallback when explicit engine chosen

Implementation

T030 [US2] CLI: Add `--stt=auto|local|openai` to `today`  
- Edit: `/Users/chris/Repos/ten-second-tom/src/Infrastructure/Cli/CommandRegistry.cs`  
- Map to `SttSelection`; pass into transcription flow

T031 [US2] Factory: enforce explicit selection rules  
- Edit: `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Services/SttProviderFactory.cs`  
- Implement no-fallback for `Local`/`OpenAI`; error messaging for missing local

Checkpoint: US2 complete.

---

## Phase 5 — US4 (P2): Review Voice Notes
Story goal: Voice notes review works like text entries; search finds transcript/summary; audio metadata visible.
Independent test: Create text + voice entries; run search; verify results and formatting.

Tests (TDD first)

T032 [US4][Tests] Rendering: voice note includes audio metadata and transcript section [P]  
- Add: `/Users/chris/Repos/ten-second-tom/tests/TenSecondTom.Tests/Features/Today/Handlers/VoiceEntryRenderingTests.cs`  
- Assert frontmatter keys; transcript `<details>`; summary

T033 [US4][Tests] Search: transcript content is searchable  
- Add: `/Users/chris/Repos/ten-second-tom/tests/TenSecondTom.Tests/Features/Search/VoiceEntrySearchTests.cs`  
- Index a sample voice entry; assert transcript terms found; verify snippet extraction shows 50 chars before/after match

T033a [US4][Tests] Search: snippet extraction for voice transcripts [P]  
- Add: `/Users/chris/Repos/ten-second-tom/tests/TenSecondTom.Tests/Features/Search/SearchSnippetExtractionTests.cs`  
- Test snippet generation: 50 characters before and after match term; handle edge cases (match near start/end of transcript)

Implementation

T034 [US4] Today formatting: ensure frontmatter + transcript block  
- Edit: `/Users/chris/Repos/ten-second-tom/src/Features/Today/Handlers/CreateVoiceNoteEntryHandler.cs`  
- Ensure metadata fields and `<details>` transcript included consistently

T035 [US4] Search integration: include transcript in searchable text  
- Edit: `/Users/chris/Repos/ten-second-tom/src/Features/Search/Handlers/SearchMemoriesQueryHandler.cs` to ensure transcript text from VoiceNoteEntry is indexed alongside summaries and titles (FR-066)  
- Confirm no duplication with existing text entries

T035a [US4] Search snippet extraction: implement context-aware snippets  
- Edit: `/Users/chris/Repos/ten-second-tom/src/Features/Search/Handlers/SearchMemoriesQueryHandler.cs` (or create SnippetExtractor service)  
- Extract 50 characters before and after match term from transcript (FR-067)  
- Handle edge cases: match at start (show 100 chars after), match at end (show 100 chars before), multiple matches (show first)

Checkpoint: US4 complete.

---

## Phase 6 — US3 (P3): Record and Store Audio with Transcription
Story goal: `tom record` records audio, transcribes, stores audio + transcript under `recording/`, supports `--json` output.
Independent test: Run `tom record` and verify files in `<memory>/recording/` and JSON output for `--json`.

Tests (TDD first)

T036 [US3][Tests] Integration: `tom record` stores files in `recording/`  
- Add: `/Users/chris/Repos/ten-second-tom/tests/TenSecondTom.IntegrationTests/Features/Audio/RecordingStorageIntegrationTests.cs`  
- Skip if ffmpeg missing; assert `.wav` and `.txt` paths and contents

T037 [US3][Tests] CLI JSON output schema  
- Add: `/Users/chris/Repos/ten-second-tom/tests/TenSecondTom.Tests/Features/Audio/Cli/RecordCliJsonTests.cs`  
- Validate `{"audio_path","transcription_path","text","duration_seconds"}`

Implementation

T038 [US3] Add `RecordCommand` and handler  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Commands/RecordCommand.cs`  
- Add: `/Users/chris/Repos/ten-second-tom/src/Features/Audio/Handlers/RecordCommandHandler.cs`  
- Orchestrate record→transcribe; always persist; return StoredRecording model
- MUST automatically create `<memory-dir>/recording/` subdirectory if it doesn't exist (FR-015)

T039 [US3] Wire CLI `tom record` with `--json`  
- Edit: `/Users/chris/Repos/ten-second-tom/src/Infrastructure/Cli/CommandRegistry.cs`  
- Add command; create `recording/` subdir if needed; print paths; JSON on flag
  - Apply `Audio:Timeouts:RecordSeconds`; optional `--max-seconds` override; prompt to continue on timeout

Checkpoint: US3 complete.

---

## Final Phase — Polish & Cross‑Cutting

T040 [Polish] Observability: ensure all audio ops log metrics (FR‑062..065)  
- Edits across providers/handlers; verify Serilog properties (engine, model, durations, word count, audio duration)
  - Include validation of periodic status messages and avoid logging content
  - Add log of timeout events and whether user continued/stopped (without logging transcript)

T041 [Polish] Docs: README, quickstart, Homebrew caveats  
- Edit: `/Users/chris/Repos/ten-second-tom/README.md` (voice setup, examples, storage growth guidance ~4.7MB per 5min)  
- Edit: `/Users/chris/Repos/ten-second-tom/specs/008-add-voice-entry/quickstart.md` (ensure steps align)  
- Update Homebrew caveats text in release tooling: legal guidance (single-user personal device), model download instructions, preprocessing config note (future use only)
- Document preprocessing configuration options (FR-016-019) as future enhancement with forward-compatible config keys (SC-016)

T042 [Polish] Example configs and env vars  
- Edit: `/Users/chris/Repos/ten-second-tom/example.appsettings.json` to include Audio settings and comments  
- Ensure env var bindings documented (e.g., `TOM_AUDIO_LOCALWHISPER_MODELPATH`)

T043 [Polish] Test fixtures: tiny 16k mono WAV sample for unit tests  
- Add: `/Users/chris/Repos/ten-second-tom/tests/TenSecondTom.Tests/TestHelpers/AudioSamples/hello-16k-mono.wav` (very short)  
- Document usage and license

---

## Dependencies (Story Order)
- US1 (P1) → enables creation of voice entries  
- US2 (P2) → builds on US1 CLI, can be implemented after US1  
- US4 (P2) → depends on US1 entries existing for review/search  
- US3 (P3) → independent of review; depends on Foundation and US1 services (recorder/STT)

Graph (stories):
- US1 → US2  
- US1 → US4  
- US1 → US3 (shared services)  
- US2, US4 parallel after US1

---

## Parallel Execution Examples

- US1 implementation parallelism:  
  - [P] T018 (FFmpeg recorder), T019 (Local STT), T020 (OpenAI STT), T021 (Factory) can proceed concurrently  
  - T023 (Handlers) can proceed after providers exist  
- US2 tests and CLI wiring (T028–T031) parallel with US4 tests (T032–T033) after US1
- US3 CLI wiring (T039) can proceed after T038; tests T036–T037 can run once CLI exists

---

## Implementation Strategy
- MVP = US1 only: `tom today --voice` with auto selection, end‑to‑end entry creation  
- Next: US2 explicit selection; in parallel, US4 review formatting/search  
- Then: US3 `tom record` for stored recordings and JSON

---

## Validation — Independent Test Criteria Per Story
- US1: Running `tom today --voice` creates a markdown note with audio metadata, transcript `<details>`, and summary; falls back to OpenAI if local unavailable  
- US2: `--stt=local` uses only local (clear error if not available); `--stt=openai` skips local  
- US4: Voice entries render with audio metadata and are discoverable by transcript terms via search  
- US3: `tom record` writes `.wav` and `.txt` to `<memory>/recording/` and prints JSON when `--json`

---

## Task Totals
- Total tasks: 45  
- Per story: US1 = 18 (T010–T027), US2 = 4 (T028–T031), US4 = 6 (T032–T035a), US3 = 4 (T036–T039)  
- Setup/Foundation/Polish: 13 (T001–T009, T040–T043)  
- Parallelizable ([P]) opportunities: T006–T008, T010–T015, T018–T021, T028–T029, T032–T033a, T036–T037
