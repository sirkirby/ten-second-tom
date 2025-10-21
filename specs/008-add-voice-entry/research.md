# Research: Voice Entry with Local-First Speech-to-Text

**Feature**: 008-add-voice-entry  
**Date**: 2025-10-20  
**Status**: Complete

## Executive Summary

This research document consolidates findings for implementing voice-based note entries with local-first speech-to-text using whisper.cpp, OpenAI STT as fallback, and ffmpeg for cross-platform audio recording. All unknowns from the technical context have been resolved with specific implementation recommendations.

## 1. Audio Recording with FFmpeg

### Decision: Use FFmpeg for Cross-Platform Audio Recording

**Rationale**:
- FFmpeg is mature, battle-tested, and available on all target platforms (macOS, Linux, Windows)
- Provides consistent command-line interface across platforms with platform-specific device drivers
- Already likely to be installed on developer machines
- Supports the exact audio format required by whisper.cpp (16kHz, mono, PCM s16le WAV)

### Platform-Specific Commands

#### macOS (AVFoundation)
```bash
# List available devices
ffmpeg -f avfoundation -list_devices true -i ""

# Record from default microphone
ffmpeg -f avfoundation -i ":0" -ar 16000 -ac 1 -c:a pcm_s16le output.wav

# Alternative: record from specific device
ffmpeg -f avfoundation -i ":1" -ar 16000 -ac 1 -c:a pcm_s16le output.wav
```

#### Linux (ALSA)
```bash
# List available devices
arecord -l

# Record from default device
ffmpeg -f alsa -i default -ar 16000 -ac 1 -c:a pcm_s16le output.wav

# Alternative: record from specific device
ffmpeg -f alsa -i hw:0 -ar 16000 -ac 1 -c:a pcm_s16le output.wav
```

#### Windows (DirectShow)
```bash
# List available devices
ffmpeg -list_devices true -f dshow -i dummy

# Record from default microphone
ffmpeg -f dshow -i audio="Microphone Array" -ar 16000 -ac 1 -c:a pcm_s16le output.wav
```

### Implementation Pattern for C#

```csharp
var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "ffmpeg",
        Arguments = GetPlatformSpecificArguments(outputPath),
        UseShellExecute = false,
        RedirectStandardInput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    }
};

process.Start();

// User presses Enter to stop
process.StandardInput.WriteLine("q"); // Graceful quit command for ffmpeg
process.WaitForExit();
```

### Graceful Termination Strategy

- **Preferred**: Send 'q' command to stdin (ffmpeg's quit command)
- **Alternative**: Use Ctrl+C signal (SIGINT on Unix, requires P/Invoke on Windows)
- **Last resort**: Process.Kill() (may corrupt WAV headers)

### Audio Format Validation

FFmpeg automatically writes proper WAV headers when terminated gracefully. Post-recording validation should check:
- File size > 44 bytes (minimum WAV header)
- Valid "RIFF" and "WAVE" markers
- Duration > 0.5 seconds (reject very short recordings)

---

## 2. Whisper.cpp Local Transcription

### Decision: Use whisper.cpp CLI as Subprocess

**Rationale**:
- C API binding would be complex and require native compilation per platform
- CLI invocation is simple, testable, and follows Unix philosophy
- Users can test whisper.cpp independently before using with Ten Second Tom
- Clear separation of concerns: recording (ffmpeg) → transcription (whisper.cpp) → summarization (OpenAI LLM)

### Model Recommendations

| Model | Disk Size | Memory | Speed | Accuracy | Recommendation |
|-------|-----------|--------|-------|----------|----------------|
| tiny | 75 MB | ~273 MB | Fastest | Lowest | ❌ Not recommended - too inaccurate for note entries |
| base | 142 MB | ~388 MB | Very fast | Good | ✅ **Recommended for default** - best balance |
| small | 466 MB | ~852 MB | Fast | Better | ✅ Good for power users |
| medium | 1.5 GB | ~2.1 GB | Moderate | High | ⚠️ Only if user has resources |
| large-v3 | 2.9 GB | ~3.9 GB | Slow | Highest | ⚠️ Overkill for note entries |

**Default Recommendation**: `ggml-base.en.bin` (English-only, 142 MB)
- Optimized for English transcription
- Faster than multilingual models
- Sufficient accuracy for personal note use
- Reasonable download size

### CLI Invocation Pattern

```bash
# Basic transcription
whisper-cpp -m models/ggml-base.en.bin -f input.wav -otxt -of output-prefix

# This creates: output-prefix.txt with plain text transcript

# Available output formats:
# -otxt : Plain text (use this)
# -osrt : SubRip subtitles
# -ovtt : WebVTT subtitles
# -ojson : JSON with word-level timestamps
```

### C# Implementation Pattern

```csharp
public async Task<Result<string>> TranscribeAsync(string audioPath, CancellationToken ct)
{
    var tempPrefix = Path.Combine(Path.GetTempPath(), $"whisper-{Guid.NewGuid()}");
    
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = _config.WhisperBinaryPath, // e.g., "whisper-cpp"
            Arguments = $"-m \"{_config.ModelPath}\" -f \"{audioPath}\" -otxt -of \"{tempPrefix}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }
    };
    
    process.Start();
    await process.WaitForExitAsync(ct);
    
    if (process.ExitCode != 0)
    {
        var error = await process.StandardError.ReadToEndAsync();
        return Result<string>.Failure($"Whisper transcription failed: {error}");
    }
    
    var transcriptPath = $"{tempPrefix}.txt";
    if (!File.Exists(transcriptPath))
    {
        return Result<string>.Failure("Whisper output file not found");
    }
    
    var transcript = await File.ReadAllTextAsync(transcriptPath, ct);
    
    // Cleanup
    File.Delete(transcriptPath);
    
    return Result<string>.Success(transcript.Trim());
}
```

### Error Handling

Common failure scenarios:
1. **Binary not found**: Check PATH, fail with installation instructions
2. **Model file missing/invalid**: Check file exists, fail with download instructions
3. **Audio format incompatible**: Pre-validate audio is 16kHz WAV
4. **Insufficient memory**: Catch exit code, suggest smaller model
5. **Corrupted audio**: Whisper may return empty transcript - detect and handle

---

## 3. OpenAI Speech-to-Text API

### Decision: Use Official OpenAI .NET SDK

**Rationale**:
- Official SDK maintained by OpenAI
- Handles authentication, retries, rate limiting automatically
- Consistent with existing LLM integration patterns in codebase
- Type-safe API with proper async support

### NuGet Package

```xml
<PackageReference Include="Azure.AI.OpenAI" Version="2.0.0" />
```

Note: The package name is `Azure.AI.OpenAI` but supports both Azure OpenAI Service and OpenAI API.

### API Usage Pattern

```csharp
using Azure.AI.OpenAI;
using Azure;

public class OpenAiSttProvider : ISttProvider
{
    private readonly OpenAIClient _client;
    private readonly string _model;
    
    public OpenAiSttProvider(string apiKey, string model = "whisper-1")
    {
        _client = new OpenAIClient(apiKey);
        _model = model;
    }
    
    public async Task<Result<string>> TranscribeAsync(
        string audioPath, 
        CancellationToken ct)
    {
        try
        {
            using var audioStream = File.OpenRead(audioPath);
            
            var options = new AudioTranscriptionOptions
            {
                AudioData = BinaryData.FromStream(audioStream),
                Filename = Path.GetFileName(audioPath),
                ResponseFormat = AudioTranscriptionFormat.Text, // Plain text
                Temperature = 0.0f // Deterministic output
            };
            
            var response = await _client.GetAudioTranscriptionAsync(
                _model, 
                options, 
                cancellationToken: ct);
            
            return Result<string>.Success(response.Value.Text);
        }
        catch (RequestFailedException ex) when (ex.Status == 429)
        {
            return Result<string>.Failure(
                "OpenAI API rate limit exceeded. Please wait and try again.");
        }
        catch (RequestFailedException ex) when (ex.Status == 401)
        {
            return Result<string>.Failure(
                "OpenAI API authentication failed. Please check your API key.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI transcription failed");
            return Result<string>.Failure($"Transcription failed: {ex.Message}");
        }
    }
}
```

### Model Options

- **whisper-1**: Standard Whisper model (recommended, most compatible)
- **gpt-4o-audio-preview**: Newer model with better accuracy (if available)

**Decision**: Default to `whisper-1` for broadest compatibility, allow override via config.

### Response Format Options

1. **Text** (recommended): Plain transcript text
2. **JSON**: Includes word-level timestamps, segments
3. **SRT**: SubRip subtitle format
4. **VTT**: WebVTT subtitle format

**Decision**: Use Text format for simplicity. JSON format could be useful for future features (speaker diarization, word-level timestamps).

### Cost Considerations

- **Pricing**: $0.006 per minute (as of 2024)
- **File size limit**: 25 MB
- **Typical note entry**: 2-5 minutes = $0.012-$0.03 per entry

The cost is minimal for personal use, but local-first approach with whisper.cpp is still preferred for privacy and offline capability.

---

## 4. Audio Preprocessing & Silence Removal

### Decision: Implement Optional Silence Removal with FFmpeg

**Rationale**:
- FFmpeg has built-in `silenceremove` filter - no additional dependencies
- Can significantly reduce file size and transcription time for recordings with long pauses
- Optional feature (configurable) to avoid complexity for MVP
- Preprocessing happens before transcription, benefiting both local and remote STT

### FFmpeg Silence Removal Command

```bash
# Remove silence from beginning and end, compress internal silence
ffmpeg -i input.wav \
  -af "silenceremove=start_periods=1:start_duration=1:start_threshold=-50dB:detection=peak,\
       silenceremove=stop_periods=-1:stop_duration=1:stop_threshold=-50dB:detection=peak,\
       silenceremove=window=0.25:stop_duration=0.5:stop_threshold=-40dB:detection=peak" \
  -ar 16000 -ac 1 -c:a pcm_s16le output.wav
```

### Configuration Parameters

```json
{
  "Audio": {
    "Preprocessing": {
      "RemoveSilence": false,  // Disabled by default for MVP
      "SilenceThresholdDb": -40,  // Lower = more aggressive
      "MinimumSilenceDurationMs": 500  // Don't remove pauses < 0.5s
    }
  }
}
```

### Implementation Strategy

**Phase 1 (MVP)**: Skip silence removal entirely
- Add configuration keys for future use
- Document in quickstart that preprocessing is planned

**Phase 2 (Future)**: Implement as optional post-recording step
- Add `AudioPreprocessor` service
- Process audio after recording, before transcription
- Log original vs. processed duration

### Alternative Considered: NAudio Library

**Rejected because**:
- Adds significant complexity (audio analysis in C#)
- FFmpeg already available as dependency
- FFmpeg's implementation is battle-tested
- Would duplicate functionality unnecessarily

---

## 5. Configuration Architecture

### Decision: Extend Existing User Secrets Pattern

**Rationale**:
- Project already uses User Secrets for sensitive config (SSH keys, API keys)
- Consistent with existing `Ssh:KeyPath`, `Llm:ApiKey` patterns
- Supports environment variables, appsettings.json, and User Secrets

### Configuration Schema

```csharp
public sealed class AudioConfiguration
{
    /// <summary>
    /// Preferred STT engine: auto, local, or openai.
    /// Default: auto (try local, fallback to OpenAI)
    /// </summary>
    public string PreferredStt { get; init; } = "auto";
    
    public RecorderConfiguration Recorder { get; init; } = new();
    public LocalWhisperConfiguration LocalWhisper { get; init; } = new();
    public OpenAiSttConfiguration OpenAI { get; init; } = new();
    public bool KeepFiles { get; init; } = true;
    public PreprocessingConfiguration Preprocessing { get; init; } = new();
}

public sealed class RecorderConfiguration
{
    public string FfmpegPath { get; init; } = "ffmpeg";
}

public sealed class LocalWhisperConfiguration
{
    public string BinaryPath { get; init; } = "whisper-cpp";
    public string? ModelPath { get; init; }  // User must provide
}

public sealed class OpenAiSttConfiguration
{
    public string Model { get; init; } = "whisper-1";
}

public sealed class PreprocessingConfiguration
{
    public bool RemoveSilence { get; init; } = false;
    public int SilenceThresholdDb { get; init; } = -40;
    public int MinimumSilenceDurationMs { get; init; } = 500;
}
```

### User Secrets Example

```json
{
  "Audio:PreferredStt": "auto",
  "Audio:Recorder:FfmpegPath": "/usr/local/bin/ffmpeg",
  "Audio:LocalWhisper:BinaryPath": "/usr/local/bin/whisper-cpp",
  "Audio:LocalWhisper:ModelPath": "/Users/chris/.models/ggml-base.en.bin",
  "Audio:OpenAI:Model": "whisper-1",
  "Audio:KeepFiles": "true",
  "Audio:Preprocessing:RemoveSilence": "false"
}
```

### First-Run UX

When `Audio:PreferredStt` is not configured:
1. Check if whisper.cpp is available (`which whisper-cpp`)
2. Prompt user: "Prefer local or OpenAI transcription? [local/openai/auto]"
3. Save preference to User Secrets: `Audio:PreferredStt`
4. If "local" chosen, prompt for model path if not configured
5. Never prompt again (respects configuration)

---

## 6. Vertical Slice Architecture Integration

### Decision: Create New "Audio" Feature Slice

**Rationale**:
- Audio recording and transcription is a distinct feature domain
- Self-contained vertical slice following project constitution
- Clear separation from "Today" and "ThisWeek" features
- Reusable by future features (e.g., "ThisWeek --voice", voice notes)

### Feature Structure

```
src/Features/Audio/
├── Commands/
│   ├── RecordAudioCommand.cs              // Start/stop recording
│   └── TranscribeAudioCommand.cs          // Transcribe existing file
├── Handlers/
│   ├── RecordAudioCommandHandler.cs       // Orchestrates recording
│   └── TranscribeAudioCommandHandler.cs   // Orchestrates transcription
├── Services/
│   ├── IAudioRecorder.cs                  // Interface
│   ├── FfmpegAudioRecorder.cs             // FFmpeg implementation
│   ├── ISttProvider.cs                    // Speech-to-text interface
│   ├── LocalWhisperSttProvider.cs         // whisper.cpp implementation
│   ├── OpenAiSttProvider.cs               // OpenAI API implementation
│   └── SttProviderFactory.cs              // Factory for STT selection
├── Models/
│   ├── AudioRecording.cs                  // Recording metadata
│   ├── TranscriptionResult.cs             // Transcription result
│   └── SttEngine.cs                       // Enum: Local, OpenAI
└── DependencyInjection.cs                 // Register services
```

### "Today" Feature Extension

```
src/Features/Today/
├── Commands/
│   └── CreateDailyEntryCommand.cs         // Add optional VoiceInput property
├── Handlers/
│   └── CreateDailyEntryHandler.cs         // Handle voice vs text input
└── ...
```

### Integration Pattern

```csharp
// In CreateDailyEntryHandler
if (!string.IsNullOrEmpty(request.VoiceInput))
{
    // Voice input provided - audio already transcribed
    userInput = request.VoiceInput;
    metadata.CustomTags["InputMethod"] = "Voice";
    metadata.CustomTags["AudioFile"] = request.AudioFilename;
    metadata.CustomTags["SttEngine"] = request.SttEngine;
}
else
{
    // Text input (existing path)
    userInput = FormatUserInput(request.Content);
    metadata.CustomTags["InputMethod"] = "Text";
}
```

---

## 7. Markdown Entry Format for Voice

### Decision: Extend Current Format with Audio Metadata Section

**Rationale**:
- Maintain consistency with existing daily entry format
- Add audio-specific metadata in frontmatter
- Use collapsible `<details>` for transcript (keeps entry concise)
- Reuse existing summary generation pipeline

### Example Voice Entry

```markdown
---
entry-id: today-10-20-2025-1
command: today
timestamp: 2025-10-20T14:30:00Z
entry-number: 1
llm-provider: OpenAI
llm-model: gpt-4
tokens-used: 1250
processing-duration: 3.2
input-method: voice
audio-file: note-20251020-143000.wav
audio-duration: 145.3
stt-engine: whisper.cpp
stt-model: ggml-base.en
---

## Audio Metadata

- **File**: `note-20251020-143000.wav`
- **Duration**: 2m 25s (145.3 seconds)
- **Transcription**: whisper.cpp (ggml-base.en)

## Transcript

<details>
<summary>Click to expand full transcript</summary>

[Full transcription text here, potentially multiple paragraphs...]

</details>

## Summary

[LLM-generated summary using existing prompt template]

### Wins

- [Extracted from transcript]

### Challenges

- [Extracted from transcript]

### Plans

- [Extracted from transcript]
```

### Backward Compatibility

Existing entries without audio metadata will continue to work:
- No `input-method` field → assumed "text"
- No `audio-file` field → no audio section rendered

---

## 8. Storage Directory Structure

### Decision: Follow Existing Command Directory Pattern

**Rationale**:
- Consistency with `today/`, `thisweek/` directories
- All memory files under configured `MemoryDirectory`
- Easy to backup, sync, or migrate entire memory directory

### Directory Layout

```
~/.memory/ten-second-tom/
├── today/
│   ├── 2025-10-20-1.md
│   ├── 2025-10-20-2.md
│   └── note-20251020-143000.wav      # Audio files alongside entries
├── thisweek/
│   └── 2025-W42.md
├── recording/                           # NEW: Raw recordings
│   ├── recording-20251020-150000.wav
│   ├── recording-20251020-150000.txt
│   ├── recording-20251020-151500.wav
│   └── recording-20251020-151500.txt
└── .templates/                          # Existing
    └── daily-summary.md
```

### File Naming Conventions

- **note audio**: `note-YYYYMMdd-HHmmss.wav` (stored in `today/`)
- **Recording audio**: `recording-YYYYMMdd-HHmmss.wav` (stored in `recording/`)
- **Transcription**: `recording-YYYYMMdd-HHmmss.txt` (stored in `recording/`)

### Audio File Lifecycle

**For `tom today --voice`**:
- `Audio:KeepFiles = true` (default): Save audio in `today/` directory
- `Audio:KeepFiles = false`: Delete audio after successful entry creation

**For `tom record`**:
- Always persist audio and transcription in `recording/` directory
- Never delete (purpose is to keep raw material for reprocessing)

---

## 9. Testing Strategy

### Decision: Multi-Tiered Testing with Conditional Integration Tests

**Rationale**:
- Unit tests don't require external dependencies (ffmpeg, whisper.cpp)
- Integration tests run conditionally based on availability
- CI/CD can skip integration tests if dependencies missing
- Developers can run full suite locally

### Test Structure

```
tests/TenSecondTom.Tests/
└── Features/
    └── Audio/
        ├── Services/
        │   ├── FfmpegAudioRecorderTests.cs       # Unit tests (mocked Process)
        │   ├── LocalWhisperSttProviderTests.cs   # Unit tests (mocked Process)
        │   ├── OpenAiSttProviderTests.cs         # Unit tests (mocked API)
        │   └── SttProviderFactoryTests.cs        # Unit tests
        └── Handlers/
            ├── RecordAudioCommandHandlerTests.cs
            └── TranscribeAudioCommandHandlerTests.cs

tests/TenSecondTom.IntegrationTests/
└── Features/
    └── Audio/
        ├── FfmpegRecordingIntegrationTests.cs    # Requires ffmpeg
        ├── WhisperTranscriptionIntegrationTests.cs  # Requires whisper.cpp + model
        └── VoiceNoteEntryIntegrationTests.cs    # End-to-end
```

### Conditional Test Execution

```csharp
[Fact]
public async Task RecordAudio_WithFFmpeg_CreatesValidWavFile()
{
    // Skip if ffmpeg not available
    if (!FfmpegAvailabilityChecker.IsAvailable())
    {
        _output.WriteLine("Skipping: ffmpeg not found on PATH");
        return;
    }
    
    // Test implementation...
}

[Fact]
public async Task TranscribeAudio_WithWhisperCpp_ReturnsTranscript()
{
    // Skip if whisper.cpp or model not available
    if (!WhisperAvailabilityChecker.IsAvailable())
    {
        _output.WriteLine("Skipping: whisper.cpp not configured");
        return;
    }
    
    // Test implementation...
}
```

### Test Data

- **Fixture audio files**: Include short WAV samples in test project
- **Format**: 16kHz, mono, PCM s16le (whisper.cpp compatible)
- **Duration**: 2-5 seconds to keep test suite fast
- **Content**: "This is a test recording" (known transcript for validation)

---

## 10. Homebrew Distribution

### Decision: Add FFmpeg as Required Dependency, Whisper.cpp as Optional

**Rationale**:
- FFmpeg is required for core audio recording functionality
- Whisper.cpp is optional (users can use OpenAI STT only)
- Homebrew automatically installs declared dependencies
- Caveats section guides users through whisper.cpp setup

### Formula Changes

```ruby
class TenSecondTom < Formula
  desc "Personal memory assistant with voice entry support"
  homepage "https://github.com/user/ten-second-tom"
  url "https://github.com/user/ten-second-tom/releases/download/v1.0.0/ten-second-tom.tar.gz"
  
  depends_on "ffmpeg"                    # NEW: Required for audio recording
  depends_on "whisper-cpp" => :optional  # NEW: Optional local STT
  
  def install
    bin.install "tom"
  end
  
  def caveats
    <<~EOS
      To use voice entry with local transcription:
      
      1. Install whisper.cpp (optional):
         brew install whisper-cpp
      
      2. Download a Whisper model:
         mkdir -p ~/.models
         curl -L -o ~/.models/ggml-base.en.bin \\
           https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin
      
      3. Configure Ten Second Tom:
         export TOM_AUDIO_LOCALWHISPER_MODELPATH=~/.models/ggml-base.en.bin
         
      Or use OpenAI transcription (no local setup required):
         tom today --voice --stt=openai
      
      For full documentation:
         https://github.com/user/ten-second-tom#voice-entry
    EOS
  end
end
```

### Installation UX

```bash
$ brew install ten-second-tom
==> Installing ten-second-tom
==> Installing dependencies: ffmpeg
==> Summary
🍺  /usr/local/Cellar/ten-second-tom/1.0.0: 5 files
==> Caveats
To use voice entry with local transcription:
[... caveats displayed ...]
```

---

## 11. Error Handling & User Feedback

### Decision: Comprehensive Error Messages with Actionable Guidance

**Rationale**:
- Voice recording involves multiple external dependencies
- Users need clear, specific guidance when things fail
- Error messages should include commands to fix the issue
- Logs provide technical details, console provides user-friendly messages

### Error Scenarios & Messages

#### 1. FFmpeg Not Found

```
❌ Error: FFmpeg not found

Voice recording requires FFmpeg for audio capture.

Installation:
  macOS:    brew install ffmpeg
  Linux:    sudo apt install ffmpeg
  Windows:  Download from https://ffmpeg.org/download.html

After installing, try again: tom today --voice
```

#### 2. Whisper.cpp Not Found (with --stt=local)

```
❌ Error: whisper.cpp not available

Local transcription requires whisper.cpp.

Installation:
  macOS:    brew install whisper-cpp
  Linux:    See https://github.com/ggerganov/whisper.cpp#quick-start
  Windows:  Download binary from https://github.com/ggerganov/whisper.cpp/releases

Or use OpenAI transcription instead: tom today --voice --stt=openai
```

#### 3. Model File Not Configured

```
❌ Error: Whisper model not configured

Local transcription requires a model file.

Download a model (recommended: base.en, 142 MB):
  mkdir -p ~/.models
  curl -L -o ~/.models/ggml-base.en.bin \\
    https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin

Configure Ten Second Tom:
  export TOM_AUDIO_LOCALWHISPER_MODELPATH=~/.models/ggml-base.en.bin

Or use setup wizard: tom setup
```

#### 4. Recording Too Short

```
⚠️  Warning: Recording too short (0.3 seconds)

Recordings shorter than 0.5 seconds may not transcribe accurately.
Please record again and speak for at least 2-3 seconds.
```

#### 5. OpenAI API Rate Limit

```
❌ Error: OpenAI rate limit exceeded

The OpenAI API rate limit has been reached.

Options:
  1. Wait a few minutes and try again
  2. Use local transcription: tom today --voice --stt=local
  3. Upgrade your OpenAI plan: https://platform.openai.com/account/billing
```

#### 6. Permission Denied (Storage)

```
❌ Error: Cannot save recording

Permission denied writing to: ~/.memory/ten-second-tom/today/

Check directory permissions:
  ls -la ~/.memory/ten-second-tom/

Fix permissions:
  chmod 755 ~/.memory/ten-second-tom/
```

### Logging Strategy

```csharp
// Log technical details
_logger.LogError(ex, 
    "FFmpeg recording failed. Exit code: {ExitCode}, Command: {Command}", 
    exitCode, command);

// Show user-friendly message
AnsiConsole.MarkupLine("[red]❌ Error:[/] FFmpeg not found");
AnsiConsole.MarkupLine("");
AnsiConsole.MarkupLine("Voice recording requires FFmpeg for audio capture.");
// ... actionable guidance ...
```

---

## 12. Future Enhancements (Out of Scope)

### Identified Opportunities

1. **`tom transcribe` Command**
   - Reprocess stored recordings from `recording/` directory
   - Try different STT providers or models
   - Batch processing: `tom transcribe --all`

2. **Prompt-Based Reprocessing**
   - Apply different prompt templates to stored transcriptions
   - Example: `tom reprocess recording/recording-20251020-150000.txt --template weekly-summary`

3. **Speaker Diarization**
   - Identify multiple speakers in recordings
   - Useful for meeting notes, interviews
   - Requires pyannote or similar library

4. **Advanced Preprocessing**
   - Noise reduction (e.g., RNNoise)
   - Audio normalization
   - Automatic gain control

5. **Voice Activity Detection (VAD)**
   - Auto-pause recording during silence
   - Resume when speech detected
   - Reduce file size and processing time

6. **Streaming Transcription**
   - Real-time transcription during recording
   - Show partial results as user speaks
   - Requires WebSocket or streaming API

7. **Multi-Language Support**
   - Detect language automatically
   - Use multilingual whisper models
   - Generate summaries in user's preferred language

---

## 13. Implementation Recommendations

### Phase 1: MVP (Core Voice Entry)

**Must Have**:
- [x] FFmpeg audio recording (macOS, Linux, Windows)
- [x] whisper.cpp local transcription
- [x] OpenAI STT fallback
- [x] Auto/local/openai STT selection
- [x] Markdown entry generation with transcript
- [x] Configuration via User Secrets
- [x] Error handling with actionable messages

**Nice to Have (if time permits)**:
- [ ] First-run STT preference prompt
- [ ] Audio file retention policy (`Audio:KeepFiles`)

### Phase 2: Recording Storage

**Must Have**:
- [x] `tom record` command
- [x] Storage in `recording/` directory
- [x] JSON output flag for scripting

### Phase 3: Audio Preprocessing (Future)

**Optional**:
- [ ] Silence removal with FFmpeg
- [ ] Configurable preprocessing thresholds
- [ ] Before/after duration logging

### Phase 4: Advanced Features (Future)

**Out of Scope for Initial Release**:
- [ ] `tom transcribe` command
- [ ] Prompt-based reprocessing
- [ ] Speaker diarization
- [ ] Streaming transcription

---

## 14. Key Dependencies

### Required NuGet Packages

```xml
<!-- Already in project -->
<PackageReference Include="System.CommandLine" Version="2.0.0-beta4.22272.1" />
<PackageReference Include="Serilog" Version="3.1.1" />

<!-- New for audio feature -->
<PackageReference Include="Azure.AI.OpenAI" Version="2.0.0" />
```

### External Tool Dependencies

| Tool | Required | Purpose | Installation |
|------|----------|---------|--------------|
| FFmpeg | Yes | Audio recording | `brew install ffmpeg` |
| whisper.cpp | No | Local transcription | `brew install whisper-cpp` |
| Whisper model | No | Local transcription | Download GGML model |

### Development Dependencies

```xml
<!-- Testing -->
<PackageReference Include="xunit" Version="2.6.1" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
<PackageReference Include="Moq" Version="4.20.69" />
```

---

## Conclusion

All technical unknowns have been resolved with specific implementation patterns:

1. ✅ **FFmpeg recording**: Platform-specific commands documented
2. ✅ **Whisper.cpp integration**: CLI invocation pattern defined
3. ✅ **OpenAI STT**: Official SDK with error handling
4. ✅ **Audio preprocessing**: FFmpeg-based silence removal (optional)
5. ✅ **Configuration**: User Secrets pattern extended
6. ✅ **Architecture**: New "Audio" feature slice
7. ✅ **Storage**: Follows existing directory structure
8. ✅ **Testing**: Multi-tier with conditional execution
9. ✅ **Distribution**: Homebrew formula updated

**Next Steps**: Proceed to Phase 1 (Design & Contracts) to generate data models and API contracts.

