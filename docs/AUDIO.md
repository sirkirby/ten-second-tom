# Audio Configuration Guide

This guide covers audio recording, preprocessing, and transcription configuration for Ten Second Tom.

## Overview

Ten Second Tom supports extensive audio configuration for different microphone types and recording environments. All settings can be configured via:

1. **Environment Variables** (recommended for production/Homebrew installs)
2. **User Secrets** (development only)
3. **appsettings.json** (fallback)

## Configuration Priority

Settings are applied in this order (highest priority first):

1. Environment variables (`TenSecondTom__*`)
2. .NET User Secrets (development only)
3. appsettings.json (defaults)

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
- **Default:** `true`
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

## Complete Environment Variable Example

For a typical MacBook Pro user:

```bash
# Audio recording settings (laptop mic optimized)
export TenSecondTom__Audio__Recorder__InputVolume=1.0
export TenSecondTom__Audio__Recorder__EnableNoiseReduction=true
export TenSecondTom__Audio__Recorder__EnableFrequencyFilters=true

# Silence removal settings
export TenSecondTom__Audio__Preprocessing__RemoveSilence=true
export TenSecondTom__Audio__Preprocessing__SilenceThresholdDb=-50
export TenSecondTom__Audio__Preprocessing__MinimumSilenceDurationMs=500
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

## Using a .env File (Development)

For local development, you can create a `.env` file in your home directory:

```bash
# ~/.tom.env
TenSecondTom__Audio__Recorder__InputVolume=0.75
TenSecondTom__Audio__Recorder__EnableNoiseReduction=false
TenSecondTom__Audio__Recorder__EnableFrequencyFilters=true
TenSecondTom__Audio__Preprocessing__RemoveSilence=true
TenSecondTom__Audio__Preprocessing__SilenceThresholdDb=-50
TenSecondTom__Audio__Preprocessing__MinimumSilenceDurationMs=500
```

Then source it before running:
```bash
source ~/.tom.env
tom record
```

## See Also

- [Configuration Guide](CONFIGURATION.md) - General configuration documentation
- [Environment Variables](ENVIRONMENT.md) - All available environment variables
- [Setup Guide](../README.md#setup) - Initial setup instructions

