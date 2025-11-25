# Audio Configuration Guide

This guide covers audio recording, preprocessing, and transcription configuration for Ten Second Tom.

## Overview

Ten Second Tom supports extensive audio configuration for different microphone types, recording environments, and speech-to-text (STT) providers. All settings can be configured via:

1. **Interactive setup wizard** (`tom config audio`) - Recommended for most users
2. **Configuration file** (`~/ten-second-tom/config/config.json`) - Automatically managed by setup wizard
3. **Environment variables** - For advanced users and CI/CD environments

### Key Features

- **Multiple STT Providers**: Support for built-in local AI, whisper-cpp, and OpenAI cloud STT
- **Built-in Local AI**: Microsoft AI Foundry Local SDK - no external dependencies required
- **Recording Optimization**: Microphone presets for different hardware types
- **Silence Removal**: Intelligent preprocessing to compress recordings
- **Noise Reduction**: Adaptive filtering for cleaner audio
- **Model Management**: CLI commands to list and download models for local providers
- **Library Transcription**: Dedicated `tom transcribe` command to re-run STT on existing audio

## Configuration Priority

Settings are applied in this order (highest priority first):

1. **Environment variables** (`TenSecondTom__Audio__*`)
2. **User configuration** (`~/ten-second-tom/config/config.json`)
3. **appsettings.json** (framework defaults only)

## Speech-to-Text (STT) Provider Configuration

### Available STT Providers

Ten Second Tom supports three speech-to-text providers:

**Built-in Local (Microsoft AI Foundry Local SDK):**
- **Pros**: No external dependencies, works offline, no API costs, privacy-focused
- **Cons**: Models must be downloaded first, requires disk space
- **Best for**: Default choice for most users, privacy-conscious workflows
- **Default:** Yes (recommended)
- **Status:** ⚠️ **Experimental** - Uses Microsoft AI Foundry Local SDK (preview). For production workloads requiring maximum stability, consider whisper.cpp or OpenAI.

**whisper.cpp (Local):**
- **Pros**: Fast local inference, no API costs, works offline, privacy-focused
- **Cons**: Requires separate installation and model download
- **Best for**: Users who already have whisper.cpp installed

**OpenAI (Cloud):**
- **Pros**: Fast, highly accurate, no local setup or storage required
- **Cons**: Requires API key, costs per minute, requires internet
- **Best for**: Best accuracy, cloud-native workflows, no local storage concerns

### STT Provider Configuration

#### Provider Selection

- **Environment Variable:** `TenSecondTom__Audio__SttProvider`
- **Type:** String (`built-in-local`, `whisper-cpp`, or `openai`)
- **Default:** `built-in-local`
- **Purpose:** Select speech-to-text provider

**Example:**
```bash
export TenSecondTom__Audio__SttProvider=built-in-local  # Default (Microsoft AI Foundry Local)
export TenSecondTom__Audio__SttProvider=whisper-cpp      # Local whisper.cpp
export TenSecondTom__Audio__SttProvider=openai           # OpenAI cloud API
```

#### Provider-Specific Configuration

Each provider can have its own configuration stored in the `Providers` dictionary. Currently, only the OpenAI provider requires additional configuration (API key).

**Configuration file format** (`~/ten-second-tom/config/config.json`):
```json
{
  "TenSecondTom": {
    "Audio": {
      "SttProvider": "built-in-local",
      "Providers": {
        "openai": {
          "ApiKey": "sk-your-openai-api-key-here"
        }
      }
    }
  }
}
```

**Environment variable** (for OpenAI):
```bash
# Note: Provider-specific settings are best managed via 'tom config audio'
# Environment variables are available but use nested syntax:
export TenSecondTom__Audio__Providers__openai__ApiKey=sk-your-api-key-here
```

### Model Management Commands

For providers that support model management (built-in-local), you can list and download models using CLI commands:

#### List Available STT Models
```bash
tom stt --list-models
tom stt --list-models --provider built-in-local
tom stt --list-models --output-json
```

#### Download STT Model
```bash
tom stt --download-model whisper-base
tom stt --download-model whisper-small --provider built-in-local
```

#### List Available LLM Models
```bash
tom llm --list-models
tom llm --list-models --provider built-in-local
tom llm --list-models --output-json
```

#### Download LLM Model
```bash
tom llm --download-model phi-3-mini
tom llm --download-model llama-3-8b --provider built-in-local
```

**Note:** Model management is currently only available for the `built-in-local` provider. Other providers (whisper-cpp, openai) do not support these commands.

### Common STT Configurations

**Built-in local (default, no configuration needed):**
```bash
export TenSecondTom__Audio__SttProvider=built-in-local
# No API key needed - works offline after model download
```

**whisper.cpp (requires separate installation):**
```bash
export TenSecondTom__Audio__SttProvider=whisper-cpp
# Requires whisper.cpp to be installed separately
```

**OpenAI cloud:**
```bash
export TenSecondTom__Audio__SttProvider=openai
# API key configured via: tom config audio
```

**Recommended:** Use `tom config audio` to configure providers interactively rather than manually editing configuration files.

## Microphone Type Presets

### Laptop/Built-in Microphones (Default)
**Best for:** MacBook Pro, laptop built-in mics, webcam mics

```bash
export TenSecondTom__Audio__Recorder__InputVolume=1.0
export TenSecondTom__Audio__Recorder__EnableNoiseReduction=true
export TenSecondTom__Audio__Recorder__EnableFrequencyFilters=true
```

### Professional Dynamic Microphones
**Best for:** Shure SM7B, Electro-Voice RE20, broadcast mics

```bash
export TenSecondTom__Audio__Recorder__InputVolume=0.75
export TenSecondTom__Audio__Recorder__EnableNoiseReduction=false
export TenSecondTom__Audio__Recorder__EnableFrequencyFilters=true
```

### Condenser/USB Microphones
**Best for:** Blue Yeti, Audio-Technica AT2020, Rode NT-USB

```bash
export TenSecondTom__Audio__Recorder__InputVolume=0.9
export TenSecondTom__Audio__Recorder__EnableNoiseReduction=false
export TenSecondTom__Audio__Recorder__EnableFrequencyFilters=true
```

### Professional Studio Setup
**Best for:** Treated rooms, professional interfaces, minimal processing needed

```bash
export TenSecondTom__Audio__Recorder__InputVolume=1.0
export TenSecondTom__Audio__Recorder__EnableNoiseReduction=false
export TenSecondTom__Audio__Recorder__EnableFrequencyFilters=false
```

## Recording Settings

### Input Volume
Controls the recording volume multiplier.

- **Environment Variable:** `TenSecondTom__Audio__Recorder__InputVolume`
- **Type:** Decimal (0.0 to 2.0)
- **Default:** `1.0`
- **Purpose:** Prevents clipping on hot mics or boosts quiet mics
- **Typical Values:**
  - `0.7-0.8`: For hot dynamic microphones
  - `1.0`: For laptop/built-in mics (no adjustment)
  - `1.0-1.2`: To boost quiet microphones

**Example:**
```bash
export TenSecondTom__Audio__Recorder__InputVolume=0.75
```

### Enable Noise Reduction
Applies FFmpeg's adaptive noise reduction filter during recording.

- **Environment Variable:** `TenSecondTom__Audio__Recorder__EnableNoiseReduction`
- **Type:** Boolean (`true` or `false`)
- **Default:** `true`
- **Purpose:** Reduces background noise, fan noise, room tone
- **Recommendation:**
  - `true`: For laptop mics, untreated rooms
  - `false`: For professional mics in treated rooms

**Example:**
```bash
export TenSecondTom__Audio__Recorder__EnableNoiseReduction=true
```

### Enable Frequency Filters
Applies high-pass and low-pass filters during recording.

- **Environment Variable:** `TenSecondTom__Audio__Recorder__EnableFrequencyFilters`
- **Type:** Boolean (`true` or `false`)
- **Default:** `true`
- **Purpose:**
  - High-pass (80Hz): Removes rumble, handling noise, HVAC vibration
  - Low-pass (8kHz): Removes high-frequency hiss (speech is below 8kHz)
- **Recommendation:**
  - `true`: For most voice recording scenarios
  - `false`: For professional studio recordings where filtering is done in post

**Example:**
```bash
export TenSecondTom__Audio__Recorder__EnableFrequencyFilters=true
```

## Preprocessing Settings (Silence Removal)

### Enable Silence Removal
Removes silence from recordings after capture, before transcription.

- **Environment Variable:** `TenSecondTom__Audio__Preprocessing__RemoveSilence`
- **Type:** Boolean (`true` or `false`)
- **Default:** `false`
- **Purpose:** Compresses long silence gaps, removes leading/trailing silence
- **Effect:**
  - Reduces file size
  - Speeds up transcription
  - Makes transcripts easier to read (no long pauses)

**Example:**
```bash
export TenSecondTom__Audio__Preprocessing__RemoveSilence=true
```

### Silence Threshold
Defines what audio level is considered "silence" (in decibels).

- **Environment Variable:** `TenSecondTom__Audio__Preprocessing__SilenceThresholdDb`
- **Type:** Integer (negative dB value)
- **Default:** `-50`
- **Purpose:** Audio below this level is considered silence
- **Typical Values:**
  - `-40dB`: Conservative (only removes very quiet silence)
  - `-50dB`: Balanced (recommended)
  - `-60dB`: Aggressive (removes more, but may clip quiet speech)

**Example:**
```bash
export TenSecondTom__Audio__Preprocessing__SilenceThresholdDb=-50
```

### Minimum Silence Duration
The minimum duration of silence before it gets removed.

- **Environment Variable:** `TenSecondTom__Audio__Preprocessing__MinimumSilenceDurationMs`
- **Type:** Integer (milliseconds)
- **Default:** `500` (0.5 seconds)
- **Purpose:** Preserves natural pauses shorter than this value
- **Typical Values:**
  - `300-400ms`: More aggressive (removes shorter pauses)
  - `500ms`: Balanced (preserves natural speech rhythm)
  - `1000ms`: Conservative (only removes long gaps)

**Example:**
```bash
export TenSecondTom__Audio__Preprocessing__MinimumSilenceDurationMs=500
```

## Recording Timeouts

Configure maximum recording durations before prompting the user to continue.

### Today Command Timeout
- **Environment Variable:** `TenSecondTom__Audio__Timeouts__TodaySeconds`
- **Type:** Integer (seconds)
- **Default:** `300` (5 minutes)
- **Purpose:** Maximum duration for `today --voice` recordings

### Record Command Timeout
- **Environment Variable:** `TenSecondTom__Audio__Timeouts__RecordSeconds`
- **Type:** Integer (seconds)
- **Default:** `1800` (30 minutes)
- **Purpose:** Maximum duration for open-ended `record` command

**Example:**
```bash
export TenSecondTom__Audio__Timeouts__TodaySeconds=600    # 10 minutes
export TenSecondTom__Audio__Timeouts__RecordSeconds=3600  # 1 hour
```

## Complete Environment Variable Example

For a typical user with built-in local STT (default configuration):

```bash
# STT Provider Configuration (built-in local is the default)
export TenSecondTom__Audio__SttProvider=built-in-local

# Audio recording settings (laptop mic optimized)
export TenSecondTom__Audio__Recorder__InputVolume=1.0
export TenSecondTom__Audio__Recorder__EnableNoiseReduction=true
export TenSecondTom__Audio__Recorder__EnableFrequencyFilters=true

# Silence removal settings (disabled by default)
export TenSecondTom__Audio__Preprocessing__RemoveSilence=false
export TenSecondTom__Audio__Preprocessing__SilenceThresholdDb=-50
export TenSecondTom__Audio__Preprocessing__MinimumSilenceDurationMs=500

# Recording timeouts
export TenSecondTom__Audio__Timeouts__TodaySeconds=300
export TenSecondTom__Audio__Timeouts__RecordSeconds=1800
```

## Troubleshooting

### Audio is Clipping/Distorted
**Symptoms:** Popping, crackling, or harsh sounds during recording

**Solution:** Lower the input volume
```bash
export TenSecondTom__Audio__Recorder__InputVolume=0.7  # Try 0.6 if still clipping
```

### Audio is Too Quiet
**Symptoms:** Transcription misses words, recording sounds muffled

**Solution:** Increase the input volume
```bash
export TenSecondTom__Audio__Recorder__InputVolume=1.2  # Boost by 20%
```

### Silence Removal Not Working
**Symptoms:** Long pauses remain in the recording, reduction shows 0%

**Solutions:**
1. Make the threshold more sensitive:
   ```bash
   export TenSecondTom__Audio__Preprocessing__SilenceThresholdDb=-60
   ```

2. Check for background noise (disable noise reduction to test):
   ```bash
   export TenSecondTom__Audio__Recorder__EnableNoiseReduction=false
   ```

3. Verify silence removal is enabled:
   ```bash
   export TenSecondTom__Audio__Preprocessing__RemoveSilence=true
   ```

### Too Much Audio Being Removed
**Symptoms:** Quiet speech is cut off, reduction percentage is very high

**Solutions:**
1. Make the threshold less sensitive:
   ```bash
   export TenSecondTom__Audio__Preprocessing__SilenceThresholdDb=-40
   ```

2. Increase minimum silence duration:
   ```bash
   export TenSecondTom__Audio__Preprocessing__MinimumSilenceDurationMs=1000
   ```

### Built-in Local STT Model Not Found
**Symptoms:** STT fails with "model not found" error

**Solutions:**
1. List available models:
   ```bash
   tom stt --list-models
   ```

2. Download the default model:
   ```bash
   tom stt --download-model whisper-base
   ```

3. Or use interactive setup:
   ```bash
   tom config audio
   ```

### OpenAI STT Failing
**Symptoms:** Transcription fails with authentication or network errors

**Solutions:**
1. Verify API key is configured:
   ```bash
   tom config show --show-secrets
   ```

2. Reconfigure API key:
   ```bash
   tom config audio
   ```

3. Check internet connectivity

## Using a .env File (Development)

For local development, you can create a `.env` file in your home directory:

```bash
# ~/.tom.env

# STT Configuration (built-in local by default)
TenSecondTom__Audio__SttProvider=built-in-local

# Recording Settings
TenSecondTom__Audio__Recorder__InputVolume=0.75
TenSecondTom__Audio__Recorder__EnableNoiseReduction=false
TenSecondTom__Audio__Recorder__EnableFrequencyFilters=true

# Preprocessing Settings
TenSecondTom__Audio__Preprocessing__RemoveSilence=true
TenSecondTom__Audio__Preprocessing__SilenceThresholdDb=-50
TenSecondTom__Audio__Preprocessing__MinimumSilenceDurationMs=500
```

Then source it before running:
```bash
source ~/.tom.env
tom record
```

**Recommended:** Use `tom config audio` for interactive configuration instead of manually creating `.env` files.

## Migration from Previous Versions

### Fallback Configuration Removed

Previous versions supported STT fallback configuration (`SttFallbackEnabled`, `SttFallbackProvider`, `SttFallbackApiKey`). This feature has been removed in favor of a simpler, single-provider model.

**If you were using fallback:**
- Your configuration will be reset on upgrade
- You'll be prompted to run `tom config audio` to reconfigure
- Choose your preferred provider (built-in-local is recommended)

**Why this changed:**
- Simplified configuration model
- Built-in local AI eliminates the need for fallback
- Users can manually switch providers if needed via `tom config audio`

## See Also

- [Configuration Guide](CONFIGURATION.md) - General configuration documentation
- [Setup Guide](../README.md#setup) - Initial setup instructions
- [CLI Reference](../README.md#commands) - Complete command reference
