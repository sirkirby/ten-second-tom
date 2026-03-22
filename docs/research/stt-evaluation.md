# STT Library Evaluation for Ten-Second Tom v2

**Date:** 2026-03-22
**Status:** Research spike complete — recommendation ready
**Spike owner:** Sprint 1

---

## Context

Ten-Second Tom v2 requires local, offline speech-to-text transcription with the following constraints (from the v2 design spec):

- **Chunked/streaming inference** — audio processed in segments up to ~5 seconds; transcript updates appear within a few seconds of speech, not sub-second word-level. Fallback: batch transcription after recording if no streaming library is viable.
- **Metal acceleration** on macOS Apple Silicon (M-series)
- **Windows support**
- **Reasonable model download size**
- **Good accuracy** for English speech
- **Clean Node.js / TypeScript API**

---

## Library Comparison Table

| Library | npm package | Streaming/Chunked | Metal (macOS) | Windows | Model sizes | Maintenance | API quality |
|---------|-------------|-------------------|---------------|---------|-------------|-------------|-------------|
| **@fugood/whisper.node** (mybigday) | `@fugood/whisper.node` | Yes — `transcribeRealtime` + new `RealtimeTranscriber` API | Yes — default variant ships Metal prebuilt | Yes — Vulkan prebuilt | tiny (75MB) → large-v3 (~3GB) | Active (published ~20 days ago as of research date) | Good — mirrors whisper.rn API |
| **@huggingface/transformers** (v3) | `@huggingface/transformers` | Partial — chunked via `chunk_length_s` param, no true push-stream API | No Metal; WASM on Node.js (WebGPU is browser-only) | Yes | distil-whisper: 600MB–1.5GB; Whisper tiny–large | Very active (HuggingFace team) | Good — pipeline API, but WASM limits throughput |
| **sherpa-onnx** | `sherpa-onnx` | Yes — first-class online/streaming transducer models | No Metal; CoreML via ONNX Runtime (limited, experimental) | Yes | Streaming models 50–400MB | Very active (published 3 days ago as of research date) | Moderate — verbose config, non-Whisper models |
| **smart-whisper** | `smart-whisper` | Batch only — no streaming API; multi-model parallel inference | Yes — auto-enables Metal on macOS | Yes | Same as whisper.cpp models | Moderately active | Good — auto model management |
| **whisper-node** (ariym) | `whisper-node` | No — file-based batch only | No | Limited | Same as whisper.cpp | Stale (last publish ~2 years ago) | Poor — spawns child process, string output |
| **nodejs-whisper** | `nodejs-whisper` | No — batch only, requires pre-existing WAV file | No | Yes (limited) | Same as whisper.cpp | Stale (~10 months) | Poor — requires audio file on disk |
| **whisper.cpp child process** | n/a (whisper.cpp binary) | Partial — can segment manually and re-invoke | Yes (via whisper.cpp Metal build) | Yes | Full range | N/A — maintained by ggerganov | Poor — shell-out, no typed API |

---

## Streaming Support Detail

### What "streaming" means for Tom v2

The spec defines chunked inference: record audio → slice into ~5-second PCM chunks → transcribe each chunk → append partial transcript to the Ink UI. This is **not** word-level streaming; it is segment-level batching with live display. Any library that accepts raw PCM buffers and returns text can satisfy this requirement.

### Per-library assessment

**@fugood/whisper.node (mybigday)**
The primary streaming path is `context.transcribeRealtime(pcmData, options)` (deprecated in v0.6+) and the new `RealtimeTranscriber` class, which pairs a `WhisperContext` with a `VadContext` and an `AudioStream`. The `RealtimeTranscriber` handles audio slicing (`audioSliceSec`), VAD-triggered boundaries, and emits transcription events per segment. This directly maps to Tom's requirement: feed PCM from the microphone, get segment transcripts as events. The newer API is the right integration point.

**@huggingface/transformers (v3)**
Supports `chunk_length_s` and `stride_length_s` parameters for batch-chunked transcription of long audio. There is no push-stream API — you pass a complete audio buffer and get back text. In Node.js, the backend is WASM (not WebGPU, which requires a browser context). This means you can build chunked inference manually by accumulating PCM buffers of N seconds and calling `pipeline()` repeatedly, but the library itself does not manage this loop. Feasible but requires more integration work. WASM performance on large models is noticeably slower than native bindings.

**sherpa-onnx**
Has genuine first-class streaming ASR via its online transducer models (Zipformer, Conformer). The Node.js addon examples include microphone streaming. The streaming models are a different model family from Whisper — they use CTC/transducer architectures optimized for low-latency chunk processing. English accuracy on these models is competitive but not identical to Whisper. Requires learning a different model ecosystem.

**smart-whisper, whisper-node, nodejs-whisper**
All batch-only. Not viable for the primary streaming path; could serve the fallback.

**whisper.cpp child process**
Technically any implementation could manually segment audio and spawn whisper.cpp for each chunk, but this introduces high per-invocation startup cost and no typed Node.js API. Not recommended as a primary approach.

---

## Detailed Notes Per Library

### @fugood/whisper.node (mybigday)

- **What it is:** Node.js bindings for whisper.cpp via NAPI, maintained by mybigday, who also maintains the React Native counterpart `whisper.rn`. The package `@fugood/whisper.node` is the npm-published distribution; the GitHub source is `mybigday/whisper.node`.
- **Prebuilt binaries:** Ships platform-specific prebuilts. macOS arm64 defaults to Metal GPU. Windows ships Vulkan prebuilts. No build step required for standard installs.
- **Streaming API:** `RealtimeTranscriber` (new) provides event-driven segment transcription with configurable slice duration and VAD. The older `transcribeRealtime` still works but is deprecated as of v0.6. For Tom v2, target the `RealtimeTranscriber` API.
- **Model compatibility:** Standard whisper.cpp GGML model files (`.bin`). All sizes supported: tiny (75MB), base (142MB), small (466MB), medium (1.5GB), large-v3 (~3GB). `ggml-distil-small.en.bin` (~380MB) is a strong default for English.
- **Windows:** Vulkan acceleration available; CPU fallback always works.
- **Concerns:** The package is a relatively small open-source project. The `whisper.rn` sibling has more stars and issues activity; `whisper.node` is less battle-tested in production Node.js environments. Monthly download counts are modest (~hundreds), not thousands.
- **npm:** `@fugood/whisper.node` — last published March 2026 (active).

### @huggingface/transformers (v3)

- **What it is:** The official HuggingFace Transformers.js library, repackaged as `@huggingface/transformers` in v3. Runs models via ONNX Runtime under the hood.
- **Node.js backend:** WASM in Node.js. WebGPU is browser-only. On macOS Apple Silicon in Node.js, models run on CPU via WASM — no Metal path.
- **Streaming:** Not a streaming API. `chunk_length_s` enables long-audio chunked batch processing but is not push-stream. Implementing Tom's live transcript requires manually buffering audio and calling the pipeline repeatedly — possible but not idiomatic.
- **Models:** Distil-Whisper (English only, 600MB–1.5GB) and standard Whisper. Wide HuggingFace model hub support.
- **Strengths:** Very high-quality models, excellent documentation, HuggingFace backing, active maintenance (thousands of npm downloads/week). Best choice if the project ever moves to browser or if Node.js WebGPU becomes mainstream.
- **Weakness for Tom v2:** No Metal acceleration in Node.js, no push-stream API, WASM throughput is slower than native bindings for real-time use cases.
- **npm:** `@huggingface/transformers` — actively maintained.

### sherpa-onnx

- **What it is:** A comprehensive speech toolkit from k2-fsa (Next-gen Kaldi project) supporting ASR, TTS, VAD, diarization, enhancement. Node.js support via NAPI addon.
- **Streaming:** Genuine first-class streaming via online transducer/CTC models. The Node.js examples include real-time microphone streaming with `OnlineRecognizer`. Segment results come back as you push audio chunks.
- **Models:** Not Whisper. Uses Zipformer, Conformer, and other CTC/transducer architectures. English streaming models exist (~50–200MB). Accuracy is competitive for English but different from Whisper's quality profile (Whisper tends to have stronger handling of accents and noisy audio).
- **GPU acceleration on macOS:** ONNX Runtime can use CoreML on Apple Silicon, but this is experimental and not enabled by default in the npm package. Effectively CPU-only in Node.js on macOS.
- **Windows:** Fully supported.
- **Maintenance:** Extremely active — k2-fsa publishes updates multiple times per week. Version 1.12.29 published March 19, 2026 (3 days before this research).
- **API:** More verbose than whisper.node — requires constructing `OnlineRecognizerConfig` with model file paths, sample rate, and feature extractor settings. Not idiomatic TypeScript but workable with wrapper code.
- **npm:** `sherpa-onnx` — very active.

### smart-whisper

- **What it is:** A whisper.cpp Node.js addon with automatic model management (download, cache, offload). Focus is on multi-model parallel inference efficiency.
- **Streaming:** Not supported. Batch transcription of audio files only.
- **Metal:** Auto-enabled on macOS via whisper.cpp's Metal path.
- **Windows:** Supported.
- **Use case:** Better fit for batch processing pipelines (e.g., post-recording transcription) than live streaming.
- **npm:** `smart-whisper` — moderately active.

### whisper-node (ariym) and nodejs-whisper

Both are batch-only, stale, and spawn child processes or require audio files on disk. Not viable for Tom v2's streaming requirement. Excluded from further consideration.

---

## Recommendation

### Primary recommendation: @fugood/whisper.node

**Use `@fugood/whisper.node` with the `RealtimeTranscriber` API.**

Rationale:

1. **Streaming works out of the box.** The `RealtimeTranscriber` API directly models Tom's requirement: PCM audio in, transcript segments out, VAD-aware slicing. No custom buffering loop required.
2. **Metal acceleration on macOS Apple Silicon** ships as the default prebuilt binary. No configuration needed.
3. **Windows support** via Vulkan prebuilt.
4. **Whisper model compatibility** means using well-understood, high-quality English speech models. Recommended default: `ggml-distil-small.en.bin` (~380MB) — good balance of accuracy and speed. Users can upgrade to `ggml-small.en.bin` or `ggml-medium.en.bin` at setup.
5. **TypeScript-friendly API** that mirrors `whisper.rn`, with the same patterns available in the React Native sibling for future mobile port.
6. **No build step** for standard targets (macOS arm64, Windows x64, Linux x64).

**Integration sketch (TranscriptionService):**

```typescript
import { initWhisper, initWhisperVad, RealtimeTranscriber } from '@fugood/whisper.node';

// In TranscriptionService constructor / setup:
const whisper = await initWhisper({ filePath: config.stt.modelPath });
const vad = await initWhisperVad({ filePath: config.stt.vadModelPath });

// During tom record:
const transcriber = new RealtimeTranscriber({
  whisperContext: whisper,
  vadContext: vad,
  audioStream: micStream,        // PCM 16kHz mono Float32
  audioSliceSec: 5,
  onTranscribe: (result) => {
    onSegment(result.data.result); // emit partial transcript to Ink UI
  },
});

await transcriber.start();
// ... user speaks ...
await transcriber.stop();
```

**Risks and mitigations:**

| Risk | Mitigation |
|------|-----------|
| Small project, low npm download volume | whisper.rn sibling is well-used in React Native ecosystem; core whisper.cpp dependency is ggerganov's actively maintained project. Worst case: fork or vendor the bindings. |
| `RealtimeTranscriber` API still evolving (deprecated old API in v0.6) | Pin to a specific version in `package.json`; write a thin `TranscriptionService` interface so swapping backends requires changing one file. |
| Prebuilt binary availability for new Node.js versions | Maintain a fallback build step in CI; document that `node-gyp` prerequisites may be needed on fresh machines. |

### Fallback strategy: Batch transcription after recording

If `@fugood/whisper.node` streaming proves unreliable in integration testing (e.g., VAD accuracy issues, prebuilt binary failures), the fallback is:

1. Record the full audio session to a WAV file.
2. Display an indeterminate progress indicator: "Transcribing…"
3. Pass the completed WAV to `@fugood/whisper.node` via `context.transcribeFile()` (batch mode), or use `smart-whisper` for the batch path.
4. Render the completed transcript on return.

This is how Ten-Second Tom v1 worked and is acceptable per the design spec. The batch API is simpler and more stable than the streaming path. If streaming does not work reliably on both macOS and Windows in Sprint 2 integration testing, drop to batch without changing the `TranscriptionService` interface — just swap the implementation.

### Alternative if whisper.node is abandoned: sherpa-onnx

If `@fugood/whisper.node` becomes unmaintained or its streaming API regresses, `sherpa-onnx` is the next best choice. It has genuine streaming, cross-platform support, and extremely active maintenance. The tradeoff is: no Metal acceleration (CPU-only on macOS in Node.js), non-Whisper models (slightly different accuracy profile), and a more verbose API requiring a wrapper layer.

---

## Model Selection for tom setup

Recommended models to offer in `tom setup` wizard, in order of preference:

| Model | File | Size | Speed (Apple M2) | Notes |
|-------|------|------|-----------------|-------|
| `ggml-distil-small.en` | distil-small.en.bin | ~380MB | Fast | English-only, 6x faster than small, recommended default |
| `ggml-small.en` | small.en.bin | ~466MB | Moderate | Good English accuracy, well-tested |
| `ggml-base.en` | base.en.bin | ~142MB | Very fast | Lower accuracy, good for low-RAM machines |
| `ggml-medium.en` | medium.en.bin | ~1.5GB | Slow without Metal | Higher accuracy, worth it on M2+ with Metal |

Default: `ggml-distil-small.en` — best accuracy/speed/size tradeoff for English speech.

---

## Approved Local Models for Agent SDK (Ollama / LM Studio)

The v2 design spec allows the Claude Agent SDK to connect to local models via Ollama's Anthropic Messages API compatibility layer (available since Ollama v0.14.0). The following models are evaluated for Tom's text analysis tasks (sentiment, summarization, context extraction).

### Summary Table

| Model | Ollama tag | RAM (Q4_K_M) | Speed on M2 (tok/s) | English analysis quality | Notes |
|-------|-----------|--------------|---------------------|--------------------------|-------|
| **qwen2.5:7b** | `qwen2.5:7b` | ~5GB | 30–45 tok/s | Excellent | Best general benchmark scores at 7B; MMLU 74.2, strong instruction following |
| **llama3.2:3b** | `llama3.2:3b` | ~2GB | 60–80 tok/s | Good | Fastest option; 3B is constrained for complex analysis but fine for sentiment |
| **llama3.2:8b** | (use llama3.1:8b) | ~5GB | 30–45 tok/s | Very good | Meta's 8B model; llama3.2 is a 3B/1B release; "8b" in Ollama resolves to llama3.1 |
| **mistral:7b** | `mistral:7b` | ~4.5GB | 35–50 tok/s | Very good | Consistent, reliable; strong reputation for instruction following at 7B |

> **Note on llama3.2:8b:** Llama 3.2 was released with 3B and 1B variants only. The 8B model in the Llama 3.x family is Llama 3.1 8B (tag `llama3.1:8b` in Ollama). If the design spec intends the 8B parameter class, use `llama3.1:8b`.

### Recommended local model: qwen2.5:7b

**Rationale:** Qwen 2.5 7B achieves the strongest benchmark scores in its parameter class (74.2 MMLU vs ~68 for Mistral 7B). Alibaba/Qwen team released a technical report showing consistent improvements on reasoning, instruction following, and structured output — all relevant for Tom's analysis tasks (structured sentiment JSON, summary generation). Runs comfortably in ~5GB RAM on Apple Silicon at 30–45 tok/s via Ollama, which is fast enough for post-recording analysis that runs in parallel with UI rendering.

**Second choice: mistral:7b** — slightly lower benchmark scores than Qwen 2.5 but an extremely stable, well-tested model with broad community verification. Good choice if Qwen 2.5 instruction-following proves inconsistent with Tom's prompts during integration testing.

**For low-RAM machines (8GB unified): llama3.2:3b** — fits in ~2GB, runs at 60–80 tok/s, and produces acceptable (not excellent) sentiment analysis. Suitable as a fallback when the user's machine cannot run a 7B model without swapping.

### Ollama integration with Claude Agent SDK

As of Ollama v0.14.0, the Anthropic Messages API is supported at `http://localhost:11434`. Configuration in `core/agent/config.ts`:

```typescript
import Anthropic from '@anthropic-ai/sdk';

// Cloud path
const cloudClient = new Anthropic({ apiKey: process.env.ANTHROPIC_API_KEY });

// Local path (Ollama)
const localClient = new Anthropic({
  baseURL: 'http://localhost:11434',
  apiKey: 'ollama',             // required field but value ignored by Ollama
});

// Usage is identical for both — Agent SDK prompting patterns work unchanged
```

Models to approve in `AppConfig.llm.modelId`: `qwen2.5:7b`, `mistral:7b`, `llama3.1:8b`, `llama3.2:3b`.

---

## Decision Record

| Question | Decision | Rationale |
|----------|----------|-----------|
| Primary STT library | `@fugood/whisper.node` | Metal on macOS, Windows prebuilts, streaming API via RealtimeTranscriber, Whisper models |
| Fallback STT strategy | Batch transcription via `context.transcribeFile()` | Matches v1 behavior, acceptable per design spec |
| Alternative if primary fails | `sherpa-onnx` | First-class streaming, extremely active, cross-platform |
| Default STT model | `ggml-distil-small.en` | Best accuracy/speed/size for English |
| Approved local LLM (primary) | `qwen2.5:7b` | Best benchmark scores at 7B class for analysis tasks |
| Approved local LLM (fallback) | `mistral:7b` | Stable, well-tested alternative |
| Approved local LLM (low-RAM) | `llama3.2:3b` | Fits 8GB unified memory machines |
| Ollama integration mechanism | Anthropic Messages API compat (v0.14.0+) | No separate provider abstraction needed; Agent SDK works unchanged |

---

## References

- [@fugood/whisper.node on npm](https://www.npmjs.com/package/@fugood/whisper.node)
- [mybigday/whisper.node on GitHub](https://github.com/mybigday/whisper.node)
- [mybigday/whisper.rn — RealtimeTranscriber API](https://github.com/mybigday/whisper.rn)
- [sherpa-onnx on npm](https://www.npmjs.com/package/sherpa-onnx)
- [k2-fsa/sherpa-onnx on GitHub](https://github.com/k2-fsa/sherpa-onnx)
- [@huggingface/transformers on npm](https://www.npmjs.com/package/@huggingface/transformers)
- [Transformers.js v3 release notes](https://huggingface.co/blog/transformersjs-v3)
- [smart-whisper on npm](https://www.npmjs.com/package/smart-whisper)
- [Ollama Anthropic API compatibility](https://ollama.com/blog/claude)
- [Qwen 2.5 Technical Report](https://arxiv.org/pdf/2412.15115)
- [Ollama VRAM requirements guide](https://localllm.in/blog/ollama-vram-requirements-for-local-llms)
- [micstream — cross-platform mic streaming for Node.js](https://micstream.dev)
