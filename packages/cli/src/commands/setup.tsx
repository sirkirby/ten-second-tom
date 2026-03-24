import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { Box, Text, useApp, useInput } from 'ink';
import SelectInput from 'ink-select-input';
import TextInput from 'ink-text-input';
import { Command } from 'commander';
import { render } from 'ink';
import { join } from 'node:path';
import {
  existsSync,
  mkdirSync,
  createWriteStream,
  unlinkSync,
  renameSync,
  statSync,
} from 'node:fs';
import { execFileSync } from 'node:child_process';
import { ConfigManager } from '@ten-second-tom/core';
import {
  DEFAULT_OLLAMA_ENDPOINT,
  DEFAULT_LOCAL_MODEL_ID,
  DEFAULT_OLLAMA_EMBEDDING_MODEL,
  DEFAULT_CLOUD_EMBEDDING_MODEL,
  ANTHROPIC_API_KEY_PREFIX,
  WHISPER_MODELS,
  getDefaultWhisperModel,
  SHERPA_MODELS,
} from '@ten-second-tom/core';
import type {
  AppConfig,
  LlmConfig,
  EmbeddingConfig,
  WhisperModel,
  SherpaModel,
} from '@ten-second-tom/core';
import { useAutoExit } from '../hooks/useAutoExit.js';
import { toErrorMessage } from '../utils/format.js';
import { EXIT_HINT_TEXT, OLLAMA_FETCH_TIMEOUT_MS } from '../constants.js';

const TOTAL_STEPS = 6;

type Step =
  | 'llm-provider'
  | 'llm-cloud-key'
  | 'llm-local-endpoint'
  | 'llm-local-model-loading'
  | 'llm-local-model'
  | 'embedding-provider'
  | 'embedding-model-loading'
  | 'embedding-model'
  | 'whisper-model'
  | 'whisper-download'
  | 'sherpa-model'
  | 'sherpa-download'
  | 'saving'
  | 'done'
  | 'error';

interface WizardState {
  llmProvider: 'cloud' | 'local' | null;
  apiKey: string;
  localEndpoint: string;
  localModelId: string;
  embeddingProvider: 'ollama' | 'cloud' | 'none' | null;
  embeddingModel: string;
  selectedWhisperModel: WhisperModel | null;
  selectedSherpaModel: SherpaModel | null;
  errorMessage: string;
  /** True when wizard was opened with an existing config loaded. */
  isReconfiguring: boolean;
  /** The whisper model id from the existing config, for "(current)" label. */
  currentWhisperModelId: string | null;
}

interface DownloadProgress {
  status: 'checking' | 'already-downloaded' | 'downloading' | 'extracting' | 'complete' | 'error';
  bytesDownloaded: number;
  totalBytes: number;
  errorMessage: string;
}

const INITIAL_DOWNLOAD_PROGRESS: DownloadProgress = {
  status: 'checking',
  bytesDownloaded: 0,
  totalBytes: 0,
  errorMessage: '',
};

const llmProviderItems = [
  { label: 'Local (Ollama / LM Studio) — runs on your machine', value: 'local' as const },
  { label: 'Claude (Anthropic) — cloud, requires API key', value: 'cloud' as const },
];

const FALLBACK_MODEL_ITEMS = [
  { label: `${DEFAULT_LOCAL_MODEL_ID} (recommended)`, value: DEFAULT_LOCAL_MODEL_ID },
  { label: 'mistral:7b', value: 'mistral:7b' },
  { label: 'llama3.2:3b', value: 'llama3.2:3b' },
];

const KNOWN_EMBEDDING_MODELS = [
  { name: 'nomic-embed-text', recommended: true, description: '768-dim, best general-purpose' },
  { name: 'bge-m3', recommended: false, description: '1024-dim, multilingual' },
  { name: 'mxbai-embed-large', recommended: false, description: '1024-dim, high accuracy' },
  { name: 'all-minilm', recommended: false, description: '384-dim, fast, lightweight' },
  {
    name: 'snowflake-arctic-embed',
    recommended: false,
    description: '1024-dim, retrieval focused',
  },
  { name: 'qwen3-embedding', recommended: false, description: 'Qwen3 embedding model' },
  { name: 'jina-embeddings', recommended: false, description: 'Jina AI embeddings' },
];

interface OllamaModel {
  name: string;
  size: number;
}

function formatBytes(bytes: number): string {
  const gb = bytes / (1024 * 1024 * 1024);
  if (gb >= 1) {
    return `${gb.toFixed(1)} GB`;
  }
  const mb = bytes / (1024 * 1024);
  if (mb >= 1) {
    return `${mb.toFixed(0)} MB`;
  }
  return `${(bytes / 1024).toFixed(0)} KB`;
}

/**
 * Fetch the list of installed models from an Ollama instance.
 * Exported for testability.
 */
export async function fetchOllamaModels(
  endpoint: string,
): Promise<{ ok: true; models: OllamaModel[] } | { ok: false; error: string }> {
  // Normalise endpoint — strip trailing slash
  const base = endpoint.replace(/\/+$/, '');
  const url = `${base}/api/tags`;

  try {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), OLLAMA_FETCH_TIMEOUT_MS);

    const response = await fetch(url, { signal: controller.signal });
    clearTimeout(timeout);

    if (!response.ok) {
      return { ok: false, error: `Ollama returned HTTP ${response.status}` };
    }

    const data = (await response.json()) as { models?: Array<{ name: string; size: number }> };
    const models: OllamaModel[] = (data.models ?? []).map((m) => ({
      name: m.name,
      size: m.size,
    }));

    return { ok: true, models };
  } catch (err) {
    if (err instanceof DOMException && err.name === 'AbortError') {
      return {
        ok: false,
        error: `Could not connect to Ollama at ${endpoint}. Connection timed out. Make sure Ollama is running.`,
      };
    }
    const msg = toErrorMessage(err);
    return {
      ok: false,
      error: `Could not connect to Ollama at ${endpoint}. Make sure Ollama is running. (${msg})`,
    };
  }
}

const embeddingProviderItems = [
  { label: 'Ollama (local vectors, recommended)', value: 'ollama' as const },
  { label: 'Cloud (Voyage AI)', value: 'cloud' as const },
  { label: 'None (keyword search only)', value: 'none' as const },
];

// ---------------------------------------------------------------------------
// Whisper model selection items
// ---------------------------------------------------------------------------

/** Build SelectInput items from the WHISPER_MODELS registry. */
function buildWhisperModelItems(): Array<{ label: string; value: string }> {
  return WHISPER_MODELS.map((m) => ({
    label: `ggml-${m.id} (${m.sizeLabel}) — ${m.description}${m.recommended ? ' [Recommended]' : ''}`,
    value: m.id,
  }));
}

// ---------------------------------------------------------------------------
// Sherpa-onnx model selection items
// ---------------------------------------------------------------------------

/** Build SelectInput items from the SHERPA_MODELS registry, plus a "Skip" option. */
function buildSherpaModelItems(): Array<{ label: string; value: string }> {
  const items = SHERPA_MODELS.map((m) => ({
    label: `${m.dirName} (${m.sizeLabel}) — ${m.description}${m.recommended ? ' [Recommended]' : ''}`,
    value: m.id,
  }));
  items.push({
    label: 'Skip — No live preview (Whisper batch transcription only)',
    value: 'skip',
  });
  return items;
}

function makeProgressBar(percent: number, width: number = 20): string {
  const filled = Math.round((percent / 100) * width);
  const empty = width - filled;
  return '[' + '\u2588'.repeat(filled) + '\u2591'.repeat(empty) + ']';
}

/**
 * Download a file with progress tracking.
 * Exported for testability.
 */
export async function downloadModel(
  url: string,
  destPath: string,
  onProgress: (bytesDownloaded: number, totalBytes: number) => void,
): Promise<void> {
  const response = await fetch(url);

  if (!response.ok) {
    throw new Error(`Download failed: HTTP ${response.status} ${response.statusText}`);
  }

  if (!response.body) {
    throw new Error('Download failed: no response body');
  }

  const contentLength = Number(response.headers.get('content-length') ?? 0);
  let bytesDownloaded = 0;

  // Ensure the directory exists
  const dir = join(destPath, '..');
  mkdirSync(dir, { recursive: true });

  // Use a temporary path so a partial download doesn't leave a corrupted file
  const tmpPath = destPath + '.downloading';

  const fileStream = createWriteStream(tmpPath);

  try {
    const reader = response.body.getReader();

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      fileStream.write(Buffer.from(value));
      bytesDownloaded += value.byteLength;
      onProgress(bytesDownloaded, contentLength);
    }

    // Wait for the write stream to finish
    await new Promise<void>((resolve, reject) => {
      fileStream.on('finish', resolve);
      fileStream.on('error', reject);
      fileStream.end();
    });

    // Rename temp file to final destination
    renameSync(tmpPath, destPath);
  } catch (err) {
    // Clean up partial download
    fileStream.end();
    try {
      unlinkSync(tmpPath);
    } catch {
      // Best effort cleanup
    }
    throw err;
  }
}

/**
 * Extract a .tar.bz2 archive to a target directory.
 * Uses the system `tar` command (available on macOS, Linux, and Windows 10+).
 */
export function extractTarBz2(archivePath: string, targetDir: string): void {
  mkdirSync(targetDir, { recursive: true });
  execFileSync('tar', ['xjf', archivePath, '-C', targetDir]);
}

export interface SetupWizardProps {
  /** When provided, called instead of exit() on completion (REPL mode). */
  onComplete?: () => void;
}

/**
 * Derive initial wizard state from an existing AppConfig (if any).
 * Returns the pre-populated WizardState and a boolean indicating whether
 * existing config was loaded.
 */
function deriveInitialState(cm: ConfigManager): { initial: WizardState; hasExisting: boolean } {
  const existing = cm.load();
  if (!existing) {
    return {
      hasExisting: false,
      initial: {
        llmProvider: null,
        apiKey: '',
        localEndpoint: DEFAULT_OLLAMA_ENDPOINT,
        localModelId: DEFAULT_LOCAL_MODEL_ID,
        embeddingProvider: null,
        embeddingModel: DEFAULT_OLLAMA_EMBEDDING_MODEL,
        selectedWhisperModel: null,
        selectedSherpaModel: null,
        errorMessage: '',
        isReconfiguring: false,
        currentWhisperModelId: null,
      },
    };
  }

  // Derive the current whisper model id from the stored model path
  const currentWhisperModelId =
    WHISPER_MODELS.find((m) => existing.stt.modelPath.endsWith(m.filename))?.id ?? null;

  return {
    hasExisting: true,
    initial: {
      llmProvider: existing.llm.provider,
      apiKey: existing.llm.provider === 'cloud' ? existing.llm.apiKey : '',
      localEndpoint:
        existing.llm.provider === 'local' ? existing.llm.localEndpoint : DEFAULT_OLLAMA_ENDPOINT,
      localModelId:
        existing.llm.provider === 'local' ? existing.llm.modelId : DEFAULT_LOCAL_MODEL_ID,
      embeddingProvider: existing.embedding.provider,
      embeddingModel:
        existing.embedding.provider !== 'none'
          ? existing.embedding.model
          : DEFAULT_OLLAMA_EMBEDDING_MODEL,
      selectedWhisperModel: null,
      selectedSherpaModel: null,
      errorMessage: '',
      isReconfiguring: true,
      currentWhisperModelId,
    },
  };
}

export function SetupWizard({ onComplete }: SetupWizardProps = {}) {
  const { exit } = useApp();
  const configManager = useMemo(() => new ConfigManager(), []);
  const { initial, hasExisting } = useMemo(
    () => deriveInitialState(configManager),
    [configManager],
  );

  const [step, setStep] = useState<Step>('llm-provider');
  const [state, setState] = useState<WizardState>(initial);
  const [ollamaModelItems, setOllamaModelItems] =
    useState<Array<{ label: string; value: string }>>(FALLBACK_MODEL_ITEMS);
  const [ollamaStatusMessage, setOllamaStatusMessage] = useState('');
  const [embeddingModelItems, setEmbeddingModelItems] = useState<
    Array<{ label: string; value: string }>
  >([]);
  const [embeddingModelStatusMessage, setEmbeddingModelStatusMessage] = useState('');
  const [whisperDownloadProgress, setWhisperDownloadProgress] =
    useState<DownloadProgress>(INITIAL_DOWNLOAD_PROGRESS);
  const [sherpaDownloadProgress, setSherpaDownloadProgress] =
    useState<DownloadProgress>(INITIAL_DOWNLOAD_PROGRESS);

  const whisperModelItems = useMemo(buildWhisperModelItems, []);
  const sherpaModelItems = useMemo(buildSherpaModelItems, []);

  // "(current)" label helpers — only annotate when reconfiguring
  const llmItems = useMemo(
    () =>
      llmProviderItems.map((item) => ({
        ...item,
        label:
          state.isReconfiguring && item.value === state.llmProvider
            ? `${item.label} (current)`
            : item.label,
      })),
    [state.isReconfiguring, state.llmProvider],
  );

  const embeddingItems = useMemo(
    () =>
      embeddingProviderItems.map((item) => ({
        ...item,
        label:
          state.isReconfiguring && item.value === state.embeddingProvider
            ? `${item.label} (current)`
            : item.label,
      })),
    [state.isReconfiguring, state.embeddingProvider],
  );

  const annotatedOllamaModelItems = useMemo(
    () =>
      ollamaModelItems.map((item) => ({
        ...item,
        label:
          state.isReconfiguring && item.value === state.localModelId
            ? `${item.label} (current)`
            : item.label,
      })),
    [ollamaModelItems, state.isReconfiguring, state.localModelId],
  );

  const annotatedEmbeddingModelItems = useMemo(
    () =>
      embeddingModelItems.map((item) => ({
        ...item,
        label:
          state.isReconfiguring && item.value === state.embeddingModel
            ? `${item.label} (current)`
            : item.label,
      })),
    [embeddingModelItems, state.isReconfiguring, state.embeddingModel],
  );

  const annotatedWhisperModelItems = useMemo(
    () =>
      whisperModelItems.map((item) => ({
        ...item,
        label:
          state.isReconfiguring && item.value === state.currentWhisperModelId
            ? `${item.label} (current)`
            : item.label,
      })),
    [whisperModelItems, state.isReconfiguring, state.currentWhisperModelId],
  );

  const annotatedSherpaModelItems = useMemo(() => {
    if (!state.isReconfiguring) return sherpaModelItems;
    const configManager = new ConfigManager();
    return sherpaModelItems.map((item) => {
      // Check if this model's directory exists on disk
      if (item.value !== 'skip' && existsSync(join(configManager.modelsPath, item.value))) {
        return { ...item, label: `${item.label} (current)` };
      }
      return item;
    });
  }, [sherpaModelItems, state.isReconfiguring]);

  // Auto-exit after error with a short delay; allow q/Enter to exit immediately.
  // Disabled in REPL mode (onComplete provided) — the REPL manages its own lifecycle.
  useAutoExit(step === 'error', undefined, !onComplete);

  useInput((input, key) => {
    if ((input === 'q' || key.return) && step === 'error') {
      if (onComplete) {
        onComplete();
      } else {
        exit();
      }
    }
    // Allow Esc to cancel setup when running inside the REPL (onComplete provided)
    if (key.escape && onComplete) {
      onComplete();
    }
  });

  function handleLlmProviderSelect(item: { value: 'cloud' | 'local' }) {
    setState((s) => ({ ...s, llmProvider: item.value }));
    if (item.value === 'cloud') {
      setStep('llm-cloud-key');
    } else {
      setStep('llm-local-endpoint');
    }
  }

  function handleApiKeySubmit(value: string) {
    const trimmed = value.trim();
    if (trimmed.length === 0) return;
    if (!trimmed.startsWith(ANTHROPIC_API_KEY_PREFIX)) {
      setState((s) => ({
        ...s,
        errorMessage:
          'Invalid API key format. Anthropic API keys start with "sk-ant-". Get your key at https://console.anthropic.com',
      }));
      setStep('error');
      return;
    }
    setState((s) => ({ ...s, apiKey: trimmed }));
    setStep('embedding-provider');
  }

  function handleLocalEndpointSubmit(value: string) {
    const endpoint = value.trim() || DEFAULT_OLLAMA_ENDPOINT;
    setState((s) => ({ ...s, localEndpoint: endpoint }));
    setStep('llm-local-model-loading');
  }

  // Fetch Ollama models when we enter the loading step
  useEffect(() => {
    if (step !== 'llm-local-model-loading') return;

    void (async () => {
      const result = await fetchOllamaModels(state.localEndpoint);

      if (!result.ok) {
        // Ollama unreachable — show error as status, fall back to hardcoded list
        setOllamaStatusMessage(result.error);
        setOllamaModelItems(FALLBACK_MODEL_ITEMS);
        setStep('llm-local-model');
        return;
      }

      if (result.models.length === 0) {
        setOllamaStatusMessage('No models found. Install a model with: ollama pull qwen2.5:7b');
        setOllamaModelItems(FALLBACK_MODEL_ITEMS);
        setStep('llm-local-model');
        return;
      }

      // Build selection items from discovered models
      const items = result.models.map((m) => ({
        label: `${m.name} (${formatBytes(m.size)})`,
        value: m.name,
      }));

      setOllamaStatusMessage('');
      setOllamaModelItems(items);
      setStep('llm-local-model');
    })();
  }, [step, state.localEndpoint]);

  function handleLocalModelSelect(item: { value: string }) {
    setState((s) => ({ ...s, localModelId: item.value }));
    setStep('embedding-provider');
  }

  // Determine the Ollama endpoint to use for embedding model discovery.
  // If the user configured a local LLM, reuse that endpoint; otherwise fall back to default.
  const embeddingOllamaEndpoint =
    state.llmProvider === 'local' ? state.localEndpoint : DEFAULT_OLLAMA_ENDPOINT;

  // Fetch Ollama models when we enter the embedding model loading step
  useEffect(() => {
    if (step !== 'embedding-model-loading') return;

    void (async () => {
      const result = await fetchOllamaModels(embeddingOllamaEndpoint);

      if (!result.ok) {
        // Ollama unreachable — show warning + manual text input fallback handled in render
        setEmbeddingModelStatusMessage(result.error);
        setEmbeddingModelItems([]);
        setStep('embedding-model');
        return;
      }

      if (result.models.length === 0) {
        setEmbeddingModelStatusMessage(
          'No models installed. Install an embedding model: `ollama pull nomic-embed-text`',
        );
        setEmbeddingModelItems([]);
        setStep('embedding-model');
        return;
      }

      // Filter to only show known embedding models — don't show LLM models
      // which would be confusing in this context
      const embeddingModels: Array<{ label: string; value: string }> = [];

      for (const m of result.models) {
        const known = KNOWN_EMBEDDING_MODELS.find((k) => m.name.startsWith(k.name));
        if (known) {
          const tag = known.recommended ? ' [Recommended]' : '';
          embeddingModels.push({
            label: `${m.name} (${formatBytes(m.size)}) — ${known.description}${tag}`,
            value: m.name,
          });
        }
      }

      if (embeddingModels.length === 0) {
        setEmbeddingModelStatusMessage(
          'No embedding models found. Install one with: ollama pull nomic-embed-text',
        );
      } else {
        setEmbeddingModelStatusMessage('');
      }
      setEmbeddingModelItems(embeddingModels);
      setStep('embedding-model');
    })();
  }, [step, embeddingOllamaEndpoint]);

  function handleEmbeddingModelSelect(item: { value: string }) {
    setState((s) => ({ ...s, embeddingModel: item.value }));
    setStep('whisper-model');
  }

  function handleEmbeddingModelManualSubmit(value: string) {
    const trimmed = value.trim();
    if (trimmed.length === 0) return;
    setState((s) => ({ ...s, embeddingModel: trimmed }));
    setStep('whisper-model');
  }

  function handleEmbeddingProviderSelect(item: { value: 'ollama' | 'cloud' | 'none' }) {
    setState((s) => ({ ...s, embeddingProvider: item.value }));
    if (item.value === 'ollama') {
      setStep('embedding-model-loading');
    } else {
      setStep('whisper-model');
    }
  }

  // ---------------------------------------------------------------------------
  // Whisper model selection
  // ---------------------------------------------------------------------------

  function handleWhisperModelSelect(item: { value: string }) {
    const model = WHISPER_MODELS.find((m) => m.id === item.value);
    if (!model) return;
    setState((s) => ({ ...s, selectedWhisperModel: model }));
    setStep('whisper-download');
  }

  // ---------------------------------------------------------------------------
  // Whisper model download
  // ---------------------------------------------------------------------------

  const whisperModelPath = useMemo(() => {
    const model = state.selectedWhisperModel ?? getDefaultWhisperModel();
    return join(configManager.modelsPath, model.filename);
  }, [state.selectedWhisperModel, configManager.modelsPath]);

  const startWhisperDownload = useCallback(async () => {
    const model = state.selectedWhisperModel ?? getDefaultWhisperModel();

    // Check if model already exists
    if (existsSync(whisperModelPath)) {
      setWhisperDownloadProgress({
        status: 'already-downloaded',
        bytesDownloaded: 0,
        totalBytes: 0,
        errorMessage: '',
      });
      // Proceed to sherpa selection after a short delay
      setTimeout(() => {
        setStep('sherpa-model');
      }, 1000);
      return;
    }

    setWhisperDownloadProgress({
      status: 'downloading',
      bytesDownloaded: 0,
      totalBytes: 0,
      errorMessage: '',
    });

    try {
      await downloadModel(model.url, whisperModelPath, (downloaded, total) => {
        setWhisperDownloadProgress({
          status: 'downloading',
          bytesDownloaded: downloaded,
          totalBytes: total,
          errorMessage: '',
        });
      });

      setWhisperDownloadProgress((prev) => ({
        ...prev,
        status: 'complete',
      }));

      // Proceed to sherpa model selection
      setTimeout(() => {
        setStep('sherpa-model');
      }, 500);
    } catch (err) {
      const msg = toErrorMessage(err);
      setWhisperDownloadProgress({
        status: 'error',
        bytesDownloaded: 0,
        totalBytes: 0,
        errorMessage: `Whisper model download failed: ${msg}`,
      });
    }
  }, [whisperModelPath, state.selectedWhisperModel]);

  useEffect(() => {
    if (step === 'whisper-download' && whisperDownloadProgress.status === 'checking') {
      void startWhisperDownload();
    }
  }, [step, whisperDownloadProgress.status, startWhisperDownload]);

  function handleWhisperDownloadErrorChoice(item: { value: string }) {
    if (item.value === 'retry') {
      setWhisperDownloadProgress(INITIAL_DOWNLOAD_PROGRESS);
    } else if (item.value === 'skip') {
      setStep('sherpa-model');
    }
  }

  // ---------------------------------------------------------------------------
  // Sherpa-onnx model selection
  // ---------------------------------------------------------------------------

  function handleSherpaModelSelect(item: { value: string }) {
    if (item.value === 'skip') {
      setState((s) => ({ ...s, selectedSherpaModel: null }));
      handleSave();
      return;
    }
    const model = SHERPA_MODELS.find((m) => m.id === item.value);
    if (!model) return;
    setState((s) => ({ ...s, selectedSherpaModel: model }));
    setStep('sherpa-download');
  }

  // ---------------------------------------------------------------------------
  // Sherpa-onnx model download
  // ---------------------------------------------------------------------------

  const startSherpaDownload = useCallback(async () => {
    const model = state.selectedSherpaModel;
    if (!model) {
      handleSave();
      return;
    }

    const modelDir = join(configManager.modelsPath, model.dirName);

    // Check if all model files already exist
    const allFilesExist =
      model.files.length > 0 && model.files.every((f) => existsSync(join(modelDir, f.filename)));
    if (allFilesExist) {
      setSherpaDownloadProgress({
        status: 'already-downloaded',
        bytesDownloaded: 0,
        totalBytes: 0,
        errorMessage: '',
      });
      setTimeout(() => {
        handleSave();
      }, 1000);
      return;
    }

    // Ensure the model directory exists
    mkdirSync(modelDir, { recursive: true });

    setSherpaDownloadProgress({
      status: 'downloading',
      bytesDownloaded: 0,
      totalBytes: 0,
      errorMessage: '',
    });

    try {
      // Download each file individually
      let totalBytesDownloaded = 0;
      for (const file of model.files) {
        const destPath = join(modelDir, file.filename);
        await downloadModel(file.url, destPath, (downloaded, _total) => {
          setSherpaDownloadProgress({
            status: 'downloading',
            bytesDownloaded: totalBytesDownloaded + downloaded,
            totalBytes: model.sizeBytes,
            errorMessage: '',
          });
        });
        // Accumulate completed file sizes
        if (existsSync(destPath)) {
          totalBytesDownloaded += statSync(destPath).size;
        }
      }

      setSherpaDownloadProgress((prev) => ({
        ...prev,
        status: 'complete',
      }));

      // Proceed to save
      setTimeout(() => {
        handleSave();
      }, 500);
    } catch (err) {
      const msg = toErrorMessage(err);
      setSherpaDownloadProgress({
        status: 'error',
        bytesDownloaded: 0,
        totalBytes: 0,
        errorMessage: `Sherpa-onnx model download failed: ${msg}`,
      });
    }
  }, [state.selectedSherpaModel, configManager.modelsPath]);

  useEffect(() => {
    if (step === 'sherpa-download' && sherpaDownloadProgress.status === 'checking') {
      void startSherpaDownload();
    }
  }, [step, sherpaDownloadProgress.status, startSherpaDownload]);

  function handleSherpaDownloadErrorChoice(item: { value: string }) {
    if (item.value === 'retry') {
      setSherpaDownloadProgress(INITIAL_DOWNLOAD_PROGRESS);
    } else if (item.value === 'skip') {
      setState((s) => ({ ...s, selectedSherpaModel: null }));
      handleSave();
    }
  }

  // ---------------------------------------------------------------------------
  // Save config
  // ---------------------------------------------------------------------------

  function handleSave() {
    setStep('saving');

    const whisperModel = state.selectedWhisperModel ?? getDefaultWhisperModel();

    const llm: LlmConfig =
      state.llmProvider === 'cloud'
        ? { provider: 'cloud', apiKey: state.apiKey }
        : {
            provider: 'local',
            localEndpoint: state.localEndpoint,
            modelId: state.localModelId,
          };

    // Use the user's local LLM endpoint for Ollama embedding when configured,
    // otherwise fall back to the default Ollama endpoint.
    const ollamaEndpoint =
      state.llmProvider === 'local' ? state.localEndpoint : DEFAULT_OLLAMA_ENDPOINT;

    const embedding: EmbeddingConfig =
      state.embeddingProvider === 'ollama'
        ? {
            provider: 'ollama',
            model: state.embeddingModel,
            endpoint: ollamaEndpoint,
          }
        : state.embeddingProvider === 'cloud'
          ? { provider: 'cloud', model: DEFAULT_CLOUD_EMBEDDING_MODEL }
          : { provider: 'none', model: '' };

    const config: AppConfig = {
      llm,
      stt: {
        engine: 'whisper.node',
        modelPath: join(configManager.modelsPath, whisperModel.filename),
      },
      embedding,
      storage: {
        dbPath: join(configManager.homePath, 'tom.db'),
      },
    };

    try {
      configManager.save(config);
      setStep('done');
      setTimeout(() => {
        if (onComplete) {
          onComplete();
        } else {
          exit();
        }
      }, 1500);
    } catch (err) {
      const code = (err as NodeJS.ErrnoException).code;
      const message =
        code === 'EACCES' || code === 'EPERM'
          ? `Permission denied writing to ${configManager.homePath}. Check that you have write access to that directory.`
          : toErrorMessage(err);
      setState((s) => ({ ...s, errorMessage: message }));
      setStep('error');
    }
  }

  // ---------------------------------------------------------------------------
  // Render helpers
  // ---------------------------------------------------------------------------

  const whisperModel = state.selectedWhisperModel ?? getDefaultWhisperModel();
  const sherpaModel = state.selectedSherpaModel;

  return (
    <Box flexDirection="column" paddingY={1}>
      <Box marginBottom={1} flexDirection="column">
        <Text bold color="cyan">
          Ten-Second Tom — Setup Wizard
        </Text>
        {hasExisting && step === 'llm-provider' && (
          <Text dimColor>Current configuration loaded. Press Enter to keep, or change.</Text>
        )}
      </Box>

      {step === 'llm-provider' && (
        <Box flexDirection="column">
          <Text>Step 1 of {TOTAL_STEPS}: Choose your LLM provider</Text>
          <Box marginTop={1}>
            <SelectInput
              items={llmItems}
              initialIndex={
                state.llmProvider
                  ? llmProviderItems.findIndex((i) => i.value === state.llmProvider)
                  : 0
              }
              onSelect={handleLlmProviderSelect}
            />
          </Box>
          {onComplete && <Text dimColor>Esc to cancel</Text>}
        </Box>
      )}

      {step === 'llm-cloud-key' && (
        <Box flexDirection="column">
          <Text>Step 2 of {TOTAL_STEPS}: Enter your Anthropic API key</Text>
          <Box marginTop={1}>
            <Text dimColor>Get your key at https://console.anthropic.com</Text>
          </Box>
          <Box marginTop={1}>
            <Text>API Key: </Text>
            <TextInput
              value={state.apiKey}
              onChange={(val) => setState((s) => ({ ...s, apiKey: val }))}
              onSubmit={handleApiKeySubmit}
              mask="*"
              placeholder="sk-ant-..."
            />
          </Box>
          {onComplete && <Text dimColor>Esc to cancel</Text>}
        </Box>
      )}

      {step === 'llm-local-endpoint' && (
        <Box flexDirection="column">
          <Text>Step 2 of {TOTAL_STEPS}: Local LLM endpoint URL</Text>
          <Box marginTop={1}>
            <Text dimColor>Press Enter to accept the default</Text>
          </Box>
          <Box marginTop={1}>
            <Text>Endpoint: </Text>
            <TextInput
              value={state.localEndpoint}
              onChange={(val) => setState((s) => ({ ...s, localEndpoint: val }))}
              onSubmit={handleLocalEndpointSubmit}
              placeholder="http://localhost:11434"
            />
          </Box>
          {onComplete && <Text dimColor>Esc to cancel</Text>}
        </Box>
      )}

      {step === 'llm-local-model-loading' && (
        <Box flexDirection="column">
          <Text>Step 2 of {TOTAL_STEPS}: Choose a local model</Text>
          <Box marginTop={1}>
            <Text dimColor>Querying Ollama for installed models...</Text>
          </Box>
        </Box>
      )}

      {step === 'llm-local-model' && (
        <Box flexDirection="column">
          <Text>Step 2 of {TOTAL_STEPS}: Choose a local model</Text>
          {ollamaStatusMessage.length > 0 && (
            <Box marginTop={1}>
              <Text color="yellow">{ollamaStatusMessage}</Text>
            </Box>
          )}
          <Box marginTop={1}>
            <SelectInput
              items={annotatedOllamaModelItems}
              initialIndex={Math.max(
                0,
                annotatedOllamaModelItems.findIndex((i) => i.value === state.localModelId),
              )}
              onSelect={handleLocalModelSelect}
            />
          </Box>
          {onComplete && <Text dimColor>Esc to cancel</Text>}
        </Box>
      )}

      {step === 'embedding-provider' && (
        <Box flexDirection="column">
          <Text>Step 3 of {TOTAL_STEPS}: Choose your embedding provider</Text>
          <Box marginTop={1}>
            <Text dimColor>Embeddings enable semantic (meaning-based) search</Text>
          </Box>
          <Box marginTop={1}>
            <SelectInput
              items={embeddingItems}
              initialIndex={
                state.embeddingProvider
                  ? embeddingProviderItems.findIndex((i) => i.value === state.embeddingProvider)
                  : 0
              }
              onSelect={handleEmbeddingProviderSelect}
            />
          </Box>
          {onComplete && <Text dimColor>Esc to cancel</Text>}
        </Box>
      )}

      {step === 'embedding-model-loading' && (
        <Box flexDirection="column">
          <Text>Step 3 of {TOTAL_STEPS}: Choose an embedding model</Text>
          <Box marginTop={1}>
            <Text dimColor>Querying Ollama for installed models...</Text>
          </Box>
        </Box>
      )}

      {step === 'embedding-model' && (
        <Box flexDirection="column">
          <Text>Step 3 of {TOTAL_STEPS}: Choose an embedding model</Text>
          {embeddingModelStatusMessage.length > 0 && (
            <Box marginTop={1}>
              <Text color="yellow">{embeddingModelStatusMessage}</Text>
            </Box>
          )}
          {embeddingModelItems.length > 0 ? (
            <Box marginTop={1}>
              <SelectInput
                items={annotatedEmbeddingModelItems}
                initialIndex={Math.max(
                  0,
                  annotatedEmbeddingModelItems.findIndex((i) => i.value === state.embeddingModel),
                )}
                onSelect={handleEmbeddingModelSelect}
              />
            </Box>
          ) : (
            <Box flexDirection="column" marginTop={1}>
              <Text dimColor>Enter model name manually (e.g. nomic-embed-text):</Text>
              <Box marginTop={1}>
                <TextInput
                  value={state.embeddingModel}
                  onChange={(val) => setState((s) => ({ ...s, embeddingModel: val }))}
                  onSubmit={handleEmbeddingModelManualSubmit}
                  placeholder={DEFAULT_OLLAMA_EMBEDDING_MODEL}
                />
              </Box>
            </Box>
          )}
          {onComplete && <Text dimColor>Esc to cancel</Text>}
        </Box>
      )}

      {step === 'whisper-model' && (
        <Box flexDirection="column">
          <Text>Step 4 of {TOTAL_STEPS}: Choose Whisper model (speech-to-text)</Text>
          <Box marginTop={1}>
            <Text dimColor>
              Whisper runs locally for private transcription. Choose a model based on your
              speed/accuracy needs.
            </Text>
          </Box>
          <Box marginTop={1}>
            <SelectInput
              items={annotatedWhisperModelItems}
              initialIndex={Math.max(
                0,
                annotatedWhisperModelItems.findIndex(
                  (i) => i.value === (state.currentWhisperModelId ?? ''),
                ),
              )}
              onSelect={handleWhisperModelSelect}
            />
          </Box>
          {onComplete && <Text dimColor>Esc to cancel</Text>}
        </Box>
      )}

      {step === 'whisper-download' && (
        <Box flexDirection="column">
          <Text>Step 4 of {TOTAL_STEPS}: Download Whisper Model</Text>
          <Box marginTop={1} flexDirection="column">
            {whisperDownloadProgress.status === 'checking' && (
              <Text dimColor>Checking for existing model...</Text>
            )}
            {whisperDownloadProgress.status === 'already-downloaded' && (
              <Text color="green">
                Model {whisperModel.filename} already downloaded. Continuing...
              </Text>
            )}
            {whisperDownloadProgress.status === 'downloading' && (
              <>
                <Text>
                  Downloading {whisperModel.filename} (~{whisperModel.sizeLabel})...
                </Text>
                <Text>
                  {'  '}
                  {whisperDownloadProgress.totalBytes > 0
                    ? `${makeProgressBar(
                        (whisperDownloadProgress.bytesDownloaded /
                          whisperDownloadProgress.totalBytes) *
                          100,
                      )} ${Math.round(
                        (whisperDownloadProgress.bytesDownloaded /
                          whisperDownloadProgress.totalBytes) *
                          100,
                      )}% (${formatBytes(whisperDownloadProgress.bytesDownloaded)} / ${formatBytes(
                        whisperDownloadProgress.totalBytes,
                      )})`
                    : `Downloaded ${formatBytes(whisperDownloadProgress.bytesDownloaded)}...`}
                </Text>
              </>
            )}
            {whisperDownloadProgress.status === 'complete' && (
              <Text color="green">Whisper model downloaded!</Text>
            )}
            {whisperDownloadProgress.status === 'error' && (
              <Box flexDirection="column">
                <Text color="red">{whisperDownloadProgress.errorMessage}</Text>
                <Box marginTop={1}>
                  <SelectInput
                    items={[
                      { label: 'Retry download', value: 'retry' },
                      { label: 'Skip (download later)', value: 'skip' },
                    ]}
                    onSelect={handleWhisperDownloadErrorChoice}
                  />
                </Box>
              </Box>
            )}
          </Box>
        </Box>
      )}

      {step === 'sherpa-model' && (
        <Box flexDirection="column">
          <Text>Step 5 of {TOTAL_STEPS}: Choose live transcription model (optional)</Text>
          <Box marginTop={1} flexDirection="column">
            <Text dimColor>
              Live transcription shows a real-time preview of your speech while recording.
            </Text>
            <Text dimColor>Requires an additional model download.</Text>
          </Box>
          <Box marginTop={1}>
            <SelectInput items={annotatedSherpaModelItems} onSelect={handleSherpaModelSelect} />
          </Box>
          {onComplete && <Text dimColor>Esc to cancel</Text>}
        </Box>
      )}

      {step === 'sherpa-download' && sherpaModel && (
        <Box flexDirection="column">
          <Text>Step 5 of {TOTAL_STEPS}: Download Live Transcription Model</Text>
          <Box marginTop={1} flexDirection="column">
            {sherpaDownloadProgress.status === 'checking' && (
              <Text dimColor>Checking for existing model...</Text>
            )}
            {sherpaDownloadProgress.status === 'already-downloaded' && (
              <Text color="green">
                Model {sherpaModel.dirName} already downloaded. Continuing...
              </Text>
            )}
            {sherpaDownloadProgress.status === 'downloading' && (
              <>
                <Text>
                  Downloading {sherpaModel.dirName} (~{sherpaModel.sizeLabel})...
                </Text>
                <Text>
                  {'  '}
                  {sherpaDownloadProgress.totalBytes > 0
                    ? `${makeProgressBar(
                        (sherpaDownloadProgress.bytesDownloaded /
                          sherpaDownloadProgress.totalBytes) *
                          100,
                      )} ${Math.round(
                        (sherpaDownloadProgress.bytesDownloaded /
                          sherpaDownloadProgress.totalBytes) *
                          100,
                      )}% (${formatBytes(sherpaDownloadProgress.bytesDownloaded)} / ${formatBytes(
                        sherpaDownloadProgress.totalBytes,
                      )})`
                    : `Downloaded ${formatBytes(sherpaDownloadProgress.bytesDownloaded)}...`}
                </Text>
              </>
            )}
            {sherpaDownloadProgress.status === 'complete' && (
              <Text color="green">Live transcription model ready!</Text>
            )}
            {sherpaDownloadProgress.status === 'error' && (
              <Box flexDirection="column">
                <Text color="red">{sherpaDownloadProgress.errorMessage}</Text>
                <Box marginTop={1}>
                  <SelectInput
                    items={[
                      { label: 'Retry download', value: 'retry' },
                      { label: 'Skip (no live preview)', value: 'skip' },
                    ]}
                    onSelect={handleSherpaDownloadErrorChoice}
                  />
                </Box>
              </Box>
            )}
          </Box>
        </Box>
      )}

      {step === 'saving' && (
        <Box>
          <Text>Saving configuration...</Text>
        </Box>
      )}

      {step === 'done' && (
        <Box flexDirection="column">
          <Text color="green" bold>
            Setup complete!
          </Text>
          <Text>Configuration saved to {configManager.homePath}/config.json</Text>
          <Text dimColor>Run `tom --help` to see available commands.</Text>
        </Box>
      )}

      {step === 'error' && (
        <Box flexDirection="column">
          <Text color="red" bold>
            Setup failed
          </Text>
          <Text color="red">{state.errorMessage}</Text>
          <Box marginTop={1}>
            <Text dimColor>{EXIT_HINT_TEXT}</Text>
          </Box>
        </Box>
      )}
    </Box>
  );
}

export const setupCommand = new Command('setup')
  .description('Configure Ten-Second Tom (LLM provider, embedding, STT)')
  .action(() => {
    render(<SetupWizard />);
  });
