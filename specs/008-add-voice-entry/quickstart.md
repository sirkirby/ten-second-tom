# Quick Start: Voice Entry Feature

**Feature**: 008-add-voice-entry  
**Last Updated**: 2025-10-20

## Overview

The voice entry feature allows you to create note entries using voice instead of typing. The system supports two transcription methods:

1. **Local transcription** using whisper.cpp (privacy-focused, offline, free)
2. **OpenAI transcription** using OpenAI's Whisper API (cloud-based, requires API key)

The system can automatically choose the best available method, or you can specify which one to use.

---

## Quick Start for Users

### Prerequisites

**Required**:
- FFmpeg (for audio recording)

**Optional** (for local transcription):
- whisper.cpp binary
- Whisper GGML model file

### Installation

#### macOS (Homebrew)

```bash
# Install Ten Second Tom (includes ffmpeg dependency)
brew install ten-second-tom

# Optional: Install whisper.cpp for local transcription
brew install whisper-cpp

# Download a Whisper model (recommended: base.en, 142 MB)
mkdir -p ~/.models
curl -L -o ~/.models/ggml-base.en.bin \
  https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin

# Configure model path
echo 'export TOM_AUDIO_LOCALWHISPER_MODELPATH=~/.models/ggml-base.en.bin' >> ~/.zshrc
source ~/.zshrc
```

#### Linux

```bash
# Install FFmpeg
sudo apt install ffmpeg  # Debian/Ubuntu
sudo yum install ffmpeg  # RedHat/CentOS

# Install whisper.cpp (optional, for local transcription)
git clone https://github.com/ggerganov/whisper.cpp.git
cd whisper.cpp
make
sudo cp main /usr/local/bin/whisper-cpp

# Download model
mkdir -p ~/.models
curl -L -o ~/.models/ggml-base.en.bin \
  https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin

# Configure
export TOM_AUDIO_LOCALWHISPER_MODELPATH=~/.models/ggml-base.en.bin
```

#### Windows

```powershell
# Install FFmpeg
# Download from https://ffmpeg.org/download.html
# Add to PATH

# Install whisper.cpp (optional)
# Download binary from https://github.com/ggerganov/whisper.cpp/releases

# Download model
mkdir $HOME\.models
Invoke-WebRequest -Uri "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin" `
  -OutFile "$HOME\.models\ggml-base.en.bin"

# Configure
$env:TOM_AUDIO_LOCALWHISPER_MODELPATH="$HOME\.models\ggml-base.en.bin"
```

### Basic Usage

#### Voice Note Entry (Most Common)

```bash
# Simple voice entry (auto-selects transcription method)
tom today --voice

# You'll see:
# Recording... press Enter to stop.
# [Recording in progress]
# [Press Enter when done]

# The system will:
# 1. Record your audio
# 2. Transcribe it (locally or via OpenAI)
# 3. Generate a summary using LLM
# 4. Save as a markdown note entry
```

#### Explicit Transcription Method

```bash
# Force local transcription only (fails if whisper.cpp not available)
tom today --voice --stt=local

# Force OpenAI transcription (uses API, costs $0.006/min)
tom today --voice --stt=openai

# Auto-select (tries local first, falls back to OpenAI)
tom today --voice --stt=auto
```

#### Store Recording for Later

```bash
# Record and transcribe, but don't create a note entry yet
tom record

# Output:
# ✓ Recording saved to: /Users/chris/.memory/ten-second-tom/recording/recording-20251020-150000.wav
# ✓ Transcription saved to: /Users/chris/.memory/ten-second-tom/recording/recording-20251020-150000.txt
# Duration: 5m 20s | Words: 287 | Engine: Local (ggml-base.en)

# Get JSON output for scripting
tom record --json
# {"audio_path": "...", "transcription_path": "...", "text": "...", "duration_seconds": 320}
```

---

## Configuration

### Via User Secrets (Recommended)

```bash
# Run setup wizard
tom setup

# Or manually configure
tom config set Audio:PreferredStt auto
tom config set Audio:LocalWhisper:ModelPath ~/.models/ggml-base.en.bin
tom config set Audio:KeepFiles true
```

### Via Environment Variables

```bash
# STT preference (auto, local, or openai)
export TOM_AUDIO_PREFERREDSTT=auto

# FFmpeg path (default: ffmpeg on PATH)
export TOM_AUDIO_RECORDER_FFMPEGPATH=/usr/local/bin/ffmpeg

# Whisper.cpp configuration
export TOM_AUDIO_LOCALWHISPER_BINARYPATH=/usr/local/bin/whisper-cpp
export TOM_AUDIO_LOCALWHISPER_MODELPATH=~/.models/ggml-base.en.bin

# OpenAI STT configuration
export TOM_AUDIO_OPENAI_MODEL=whisper-1

# Keep audio files after transcription (true/false)
export TOM_AUDIO_KEEPFILES=true
```

### Via appsettings.json (Development)

```json
{
  "TenSecondTom": {
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
      "KeepFiles": true
    }
  }
}
```

---

## Whisper Model Selection

### Recommended Models

| Model | Size | Speed | Accuracy | Use Case |
|-------|------|-------|----------|----------|
| **base.en** | 142 MB | Very Fast | Good | ✅ **Recommended** - Best balance for note entries |
| tiny.en | 75 MB | Fastest | Lower | ⚠️ Too inaccurate for most uses |
| small.en | 466 MB | Fast | Better | Good for power users |
| medium.en | 1.5 GB | Moderate | High | Only if you have resources |
| large-v3 | 2.9 GB | Slow | Highest | ⚠️ Overkill for note entries |

### Model Download Locations

**Official Hugging Face (Recommended)**:
```bash
# Base English (recommended)
https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin

# Other models
https://huggingface.co/ggerganov/whisper.cpp/tree/main
```

**Alternative: Use whisper.cpp download script**:
```bash
cd whisper.cpp
sh ./models/download-ggml-model.sh base.en
```

---

## Example Workflows

### Workflow 1: Daily Voice Check-In

```bash
# Quick daily voice entry
tom today --voice

# Recording... press Enter to stop.
# [Speak for 2-3 minutes about your day]
# [Press Enter]

# ✓ Transcribed using Local (ggml-base.en) in 8.5s
# ✓ Generated summary using OpenAI (gpt-4)
# ✓ Entry saved: today/2025-10-20-1.md
```

### Workflow 2: Meeting Notes

```bash
# Record meeting without creating note entry
tom record

# Recording... press Enter to stop.
# [Record entire meeting]

# Later: Process the recording with different prompts
# (Future feature - architecture supports this)
```

### Workflow 3: Privacy-First Voice Entry

```bash
# Force local transcription (never sends audio to cloud)
tom today --voice --stt=local

# Uses only local whisper.cpp, no API calls
```

---

## Troubleshooting

### "FFmpeg not found"

**Problem**: FFmpeg is required for audio recording but not installed.

**Solution**:
```bash
# macOS
brew install ffmpeg

# Linux
sudo apt install ffmpeg

# Windows
# Download from https://ffmpeg.org/download.html
```

### "whisper.cpp not available"

**Problem**: Trying to use `--stt=local` without whisper.cpp installed.

**Solutions**:
1. Install whisper.cpp (see Installation section above)
2. Use OpenAI instead: `tom today --voice --stt=openai`
3. Use auto-fallback: `tom today --voice --stt=auto`

### "Whisper model not configured"

**Problem**: whisper.cpp is installed but model path not set.

**Solution**:
```bash
# Download model
mkdir -p ~/.models
curl -L -o ~/.models/ggml-base.en.bin \
  https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin

# Configure path
export TOM_AUDIO_LOCALWHISPER_MODELPATH=~/.models/ggml-base.en.bin

# Or use setup wizard
tom setup
```

### "Recording too short"

**Problem**: Recording is less than 0.5 seconds.

**Solution**: Speak for at least 2-3 seconds before pressing Enter.

### "OpenAI rate limit exceeded"

**Problem**: Too many API calls to OpenAI in short time.

**Solutions**:
1. Wait a few minutes and try again
2. Use local transcription: `--stt=local`
3. Upgrade OpenAI plan (if needed)

### "Permission denied"

**Problem**: Cannot write to memory directory.

**Solution**:
```bash
# Check permissions
ls -la ~/.memory/ten-second-tom/

# Fix permissions
chmod 755 ~/.memory/ten-second-tom/
chmod 755 ~/.memory/ten-second-tom/today/
chmod 755 ~/.memory/ten-second-tom/recording/
```

---

## Voice Entry Output Format

### Markdown Entry Structure

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
stt-engine: Local
stt-model: ggml-base.en
---

## Audio Metadata

- **File**: `note-20251020-143000.wav`
- **Duration**: 2m 25s (145.3 seconds)
- **Transcription**: whisper.cpp (ggml-base.en)

## Transcript

<details>
<summary>Click to expand full transcript</summary>

[Full transcription text here]

</details>

## Summary

[LLM-generated summary]

### Wins

- [Extracted wins]

### Challenges

- [Extracted challenges]

### Plans

- [Plans for tomorrow]
```

### JSON Output (for scripting)

```json
{
  "audio_path": "/Users/chris/.memory/ten-second-tom/recording/recording-20251020-150000.wav",
  "transcription_path": "/Users/chris/.memory/ten-second-tom/recording/recording-20251020-150000.txt",
  "text": "Full transcript text...",
  "duration_seconds": 320.5,
  "word_count": 287,
  "stt_engine": "Local",
  "stt_model": "ggml-base.en",
  "recorded_at": "2025-10-20T15:00:00Z",
  "file_size_bytes": 10255360
}
```

---

## Storage Locations

### note Entries with Audio

```
~/.memory/ten-second-tom/
└── today/
    ├── 2025-10-20-1.md              # Markdown entry
    └── note-20251020-143000.wav    # Audio file (if KeepFiles=true)
```

### Stored Recordings (tom record)

```
~/.memory/ten-second-tom/
└── recording/
    ├── recording-20251020-150000.wav  # Audio file (always persisted)
    └── recording-20251020-150000.txt  # Transcript (always persisted)
```

---

## Cost Comparison

### Local Transcription (whisper.cpp)

- **Cost**: Free
- **Privacy**: 100% local, never leaves your machine
- **Speed**: ~0.2-0.5x realtime (depends on CPU/GPU)
- **Setup**: Requires whisper.cpp + model download (~142 MB)
- **Best for**: Privacy-conscious users, offline use, unlimited usage

### OpenAI Transcription

- **Cost**: $0.006 per minute (~$0.03 for 5-minute note entry)
- **Privacy**: Audio sent to OpenAI API
- **Speed**: ~0.1x realtime (depends on network)
- **Setup**: Just need OpenAI API key (no local installation)
- **Best for**: Quick setup, highest accuracy, occasional use

**Example Monthly Costs**:
- Daily 5-minute voice entry via OpenAI: ~$0.90/month (30 days × 5 min × $0.006)
- Daily 5-minute voice entry via local: $0/month (free)

---

## Best Practices

### Recording Quality

1. **Quiet Environment**: Record in a quiet room to improve transcription accuracy
2. **Microphone Proximity**: Speak 6-12 inches from microphone
3. **Clear Speech**: Speak clearly at normal pace (not too fast)
4. **Minimal Pauses**: Long pauses increase file size and processing time
5. **Recording Length**: 2-5 minutes is ideal for note entries

### Privacy Considerations

- Use `--stt=local` for sensitive content (never sends audio to cloud)
- Set `Audio:KeepFiles=false` to auto-delete audio after transcription
- Recordings in `recording/` are never auto-deleted (manual cleanup)

### Performance Tips

- Use `base.en` model for best speed/accuracy balance
- Local transcription is faster for recordings < 10 minutes
- OpenAI transcription is faster for very long recordings (> 30 minutes)

---

## Legal & Privacy Considerations

### Single-User Personal Device Assumption

Ten Second Tom's voice feature is designed for **personal note use on your own device**, recording **your own voice** for your own personal records. In this context:

- ✅ **No consent UI required** - You're recording yourself on your own device
- ✅ **No legal restrictions** - Personal voice memos are legal everywhere
- ✅ **Privacy-focused** - Local transcription option keeps everything on your device

### Recording Others

⚠️ **If you plan to record other people** (meetings, interviews, etc.), you are responsible for:

1. **Obtaining consent** - Many jurisdictions require consent to record conversations
   - **One-party consent** (e.g., most US states): Only you need to consent
   - **Two-party consent** (e.g., California, Florida, some European countries): All parties must consent
2. **Complying with local laws** - Recording laws vary by jurisdiction
3. **Protecting privacy** - Ensure appropriate handling of others' voices and data

### Data Protection

- **Local transcription** (`--stt=local`): Audio never leaves your device (100% private)
- **OpenAI transcription** (`--stt=openai`): Audio sent to OpenAI API (covered by OpenAI's data policies)
- **Storage**: All audio files stored locally in your configured memory directory
- **Logs**: System never logs audio content or transcript text (only metadata)

For more information:
- OpenAI Data Privacy: https://openai.com/policies/privacy-policy
- whisper.cpp (local): Fully offline, no data transmission

---

## Storage Management

### Understanding Storage Growth

Voice recordings use approximately **4.7 MB per 5-minute recording** (16kHz mono WAV format).

**Monthly Estimates**:
- 1 voice entry/day (5 min avg): ~141 MB/month
- 2 voice entries/day: ~282 MB/month
- Daily + stored recordings: Variable (depends on usage)

### Audio File Retention

**note Entries** (`tom today --voice`):
- Controlled by `Audio:KeepFiles` configuration (default: `true`)
- Set to `false` to auto-delete audio after successful transcription
- Markdown entry with transcript and summary always kept

**Stored Recordings** (`tom record`):
- Always persisted (never auto-deleted)
- Designed for building an audio library for future reprocessing

### Manual Cleanup Procedures

**To remove old note audio files**:
```bash
# View note audio files
ls -lh ~/.memory/ten-second-tom/today/*.wav

# Remove audio files older than 90 days (keeps markdown entries)
find ~/.memory/ten-second-tom/today -name "*.wav" -mtime +90 -delete

# Or remove all audio files (keeps transcripts and summaries)
rm ~/.memory/ten-second-tom/today/*.wav
```

**To remove stored recordings**:
```bash
# View stored recordings
ls -lh ~/.memory/ten-second-tom/recording/

# Remove old recordings (audio + transcripts)
find ~/.memory/ten-second-tom/recording -name "recording-2024*" -delete
```

**To check total storage usage**:
```bash
# Total audio storage
du -sh ~/.memory/ten-second-tom/today/*.wav
du -sh ~/.memory/ten-second-tom/recording/

# All Ten Second Tom data
du -sh ~/.memory/ten-second-tom/
```

### Automatic Cleanup (Not Implemented)

The system does **not** implement automatic storage quotas or retention policies. This is intentional:

- ✅ **User control**: You decide what to keep
- ✅ **Simplicity**: No complex cleanup logic
- ✅ **Safety**: Never accidentally delete valuable recordings

**Future Enhancement**: A `tom cleanup` command may be added in the future for convenient storage management.

---

## FAQ

**Q: Can I use voice entry without internet?**  
A: Yes, if you have whisper.cpp and a model installed locally. Use `--stt=local`.

**Q: Which is more accurate: local or OpenAI?**  
A: OpenAI is generally more accurate, especially for accents and difficult audio. However, whisper.cpp with base.en or higher is very good for clear speech.

**Q: Can I reprocess old recordings with different models?**  
A: This is planned for a future release. The architecture supports it (that's why `tom record` saves both audio and transcript).

**Q: Does the system support multiple speakers?**  
A: Currently no. Speaker diarization is planned for a future release.

**Q: What audio format is required?**  
A: For local (whisper.cpp): WAV, 16kHz, mono. The system automatically records in this format.
For OpenAI: WAV, MP3, M4A all supported.

**Q: Can I transcribe existing audio files?**  
A: Not directly in the initial release. Planned for future `tom transcribe` command.

**Q: How do I switch between local and OpenAI?**  
A: Use the `--stt` flag: `--stt=local`, `--stt=openai`, or `--stt=auto` (default).

**Q: Where are my audio files stored?**  
A: note audio: `~/.memory/ten-second-tom/today/` (if KeepFiles=true)  
Recording audio: `~/.memory/ten-second-tom/recording/` (always kept)

---

## Next Steps

1. **Install prerequisites** (ffmpeg, optional whisper.cpp)
2. **Download a model** (if using local transcription)
3. **Try your first voice entry**: `tom today --voice`
4. **Review the markdown entry** to see the full transcript and summary
5. **Experiment with different STT options** to find your preference

For more details, see:
- Full specification: `/specs/008-add-voice-entry/spec.md`
- Research document: `/specs/008-add-voice-entry/research.md`
- Data model: `/specs/008-add-voice-entry/data-model.md`

---

**Version**: 1.0.0  
**Last Updated**: 2025-10-20

