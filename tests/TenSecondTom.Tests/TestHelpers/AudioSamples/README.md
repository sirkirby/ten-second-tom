# Audio Test Samples

This directory contains test audio samples used for testing Ten Second Tom's audio recording and transcription features.

## Files

### hello-16k-mono.wav

A minimal test audio file for unit and integration tests.

**Specifications:**
- Sample Rate: 16kHz
- Channels: Mono (1)
- Encoding: PCM s16le (signed 16-bit little-endian)
- Duration: 2-3 seconds
- Format: WAV

**Purpose:**
Used in audio feature tests to verify:
- Audio recording functionality
- FFmpeg integration
- Whisper.cpp compatibility
- File format validation

## Generating Test Samples

If you need to regenerate the test audio file, use ffmpeg:

```bash
ffmpeg -f lavfi -i "sine=frequency=1000:duration=2" \
  -ar 16000 \
  -ac 1 \
  -c:a pcm_s16le \
  hello-16k-mono.wav
```

This generates a 2-second sine wave tone at 1000Hz with the correct format for whisper.cpp.

## License

These test files are generated programmatically and contain no copyrighted content. They are provided as-is for testing purposes only.

## Usage in Tests

```csharp
var testAudioPath = Path.Combine(
    TestContext.CurrentContext.TestDirectory,
    "TestHelpers",
    "AudioSamples",
    "hello-16k-mono.wav"
);

// Use in tests...
```

**Note:** If the test file is missing, tests that depend on it should be marked with `[Ignore("Test audio file not available")]` or generate the file dynamically.
