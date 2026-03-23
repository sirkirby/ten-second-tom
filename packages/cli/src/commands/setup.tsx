import React, { useState, useEffect, useCallback } from 'react';
import { Box, Text, useApp, useInput } from 'ink';
import SelectInput from 'ink-select-input';
import TextInput from 'ink-text-input';
import { Command } from 'commander';
import { render } from 'ink';
import { join } from 'node:path';
import { existsSync, mkdirSync, createWriteStream, unlinkSync } from 'node:fs';
import { ConfigManager } from '@ten-second-tom/core';
import type { AppConfig, LlmConfig, EmbeddingConfig } from '@ten-second-tom/core';
import { useAutoExit } from '../hooks/useAutoExit.js';

const WHISPER_MODEL_URL =
  'https://huggingface.co/distil-whisper/distil-small.en/resolve/main/ggml-distil-small.en.bin';
const WHISPER_MODEL_FILENAME = 'ggml-distil-small.en.bin';

type Step =
  | 'llm-provider'
  | 'llm-cloud-key'
  | 'llm-local-endpoint'
  | 'llm-local-model-loading'
  | 'llm-local-model'
  | 'embedding-provider'
  | 'stt-info'
  | 'model-download'
  | 'saving'
  | 'done'
  | 'error';

interface WizardState {
  llmProvider: 'cloud' | 'local' | null;
  apiKey: string;
  localEndpoint: string;
  localModelId: string;
  embeddingProvider: 'ollama' | 'cloud' | 'none' | null;
  errorMessage: string;
}

interface DownloadProgress {
  status: 'checking' | 'already-downloaded' | 'downloading' | 'complete' | 'error';
  bytesDownloaded: number;
  totalBytes: number;
  errorMessage: string;
}

const llmProviderItems = [
  { label: 'Cloud (Claude via Anthropic API)', value: 'cloud' as const },
  { label: 'Local (Ollama / LM Studio)', value: 'local' as const },
];

const FALLBACK_MODEL_ITEMS = [
  { label: 'qwen2.5:7b (recommended)', value: 'qwen2.5:7b' },
  { label: 'mistral:7b', value: 'mistral:7b' },
  { label: 'llama3.2:3b', value: 'llama3.2:3b' },
];

interface OllamaModel {
  name: string;
  size: number;
}

function formatModelSize(bytes: number): string {
  const gb = bytes / (1024 * 1024 * 1024);
  if (gb >= 1) {
    return `${gb.toFixed(1)} GB`;
  }
  return `${(bytes / (1024 * 1024)).toFixed(0)} MB`;
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
    const timeout = setTimeout(() => controller.abort(), 5000);

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
    const msg = err instanceof Error ? err.message : String(err);
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

function formatBytes(bytes: number): string {
  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(0)} KB`;
  }
  return `${(bytes / (1024 * 1024)).toFixed(0)} MB`;
}

function makeProgressBar(percent: number, width: number = 20): string {
  const filled = Math.round((percent / 100) * width);
  const empty = width - filled;
  return '[' + '\u2588'.repeat(filled) + '\u2591'.repeat(empty) + ']';
}

/**
 * Download the Whisper model file with progress tracking.
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
    const { renameSync } = await import('node:fs');
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

function SetupWizard() {
  const { exit } = useApp();
  const [step, setStep] = useState<Step>('llm-provider');
  const [state, setState] = useState<WizardState>({
    llmProvider: null,
    apiKey: '',
    localEndpoint: 'http://localhost:11434',
    localModelId: 'qwen2.5:7b',
    embeddingProvider: null,
    errorMessage: '',
  });
  const [ollamaModelItems, setOllamaModelItems] =
    useState<Array<{ label: string; value: string }>>(FALLBACK_MODEL_ITEMS);
  const [ollamaStatusMessage, setOllamaStatusMessage] = useState('');
  const [downloadProgress, setDownloadProgress] = useState<DownloadProgress>({
    status: 'checking',
    bytesDownloaded: 0,
    totalBytes: 0,
    errorMessage: '',
  });

  const configManager = new ConfigManager();

  // Auto-exit after error with a short delay; allow q/Enter to exit immediately
  useAutoExit(step === 'error');

  useInput((input, key) => {
    if ((input === 'q' || key.return) && step === 'error') {
      exit();
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
    if (!trimmed.startsWith('sk-ant-')) {
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
    const endpoint = value.trim() || 'http://localhost:11434';
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
        label: `${m.name} (${formatModelSize(m.size)})`,
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

  function handleEmbeddingProviderSelect(item: { value: 'ollama' | 'cloud' | 'none' }) {
    setState((s) => ({ ...s, embeddingProvider: item.value }));
    setStep('stt-info');
  }

  function handleSttContinue(item: { value: string }) {
    if (item.value === 'continue') {
      setStep('model-download');
    }
  }

  // -------------------------------------------------------------------------
  // Model download step
  // -------------------------------------------------------------------------
  const modelPath = join(configManager.modelsPath, WHISPER_MODEL_FILENAME);

  const startDownload = useCallback(async () => {
    // Check if model already exists
    if (existsSync(modelPath)) {
      setDownloadProgress({
        status: 'already-downloaded',
        bytesDownloaded: 0,
        totalBytes: 0,
        errorMessage: '',
      });
      // Proceed to save after a short delay
      setTimeout(() => {
        handleSave();
      }, 1000);
      return;
    }

    setDownloadProgress({
      status: 'downloading',
      bytesDownloaded: 0,
      totalBytes: 0,
      errorMessage: '',
    });

    try {
      await downloadModel(WHISPER_MODEL_URL, modelPath, (downloaded, total) => {
        setDownloadProgress({
          status: 'downloading',
          bytesDownloaded: downloaded,
          totalBytes: total,
          errorMessage: '',
        });
      });

      setDownloadProgress((prev) => ({
        ...prev,
        status: 'complete',
      }));

      // Proceed to save
      handleSave();
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      setDownloadProgress({
        status: 'error',
        bytesDownloaded: 0,
        totalBytes: 0,
        errorMessage: `Model download failed: ${msg}`,
      });
    }
  }, [modelPath]);

  useEffect(() => {
    if (step === 'model-download' && downloadProgress.status === 'checking') {
      void startDownload();
    }
  }, [step, downloadProgress.status, startDownload]);

  // Handle retry/skip on download error
  function handleDownloadErrorChoice(item: { value: string }) {
    if (item.value === 'retry') {
      setDownloadProgress({
        status: 'checking',
        bytesDownloaded: 0,
        totalBytes: 0,
        errorMessage: '',
      });
    } else if (item.value === 'skip') {
      handleSave();
    }
  }

  function handleSave() {
    setStep('saving');

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
      state.llmProvider === 'local' ? state.localEndpoint : 'http://localhost:11434';

    const embedding: EmbeddingConfig =
      state.embeddingProvider === 'ollama'
        ? {
            provider: 'ollama',
            model: 'nomic-embed-text',
            endpoint: ollamaEndpoint,
          }
        : state.embeddingProvider === 'cloud'
          ? { provider: 'cloud', model: 'voyage-3-lite' }
          : { provider: 'none', model: '' };

    const config: AppConfig = {
      llm,
      stt: {
        engine: 'whisper.node',
        modelPath: join(configManager.modelsPath, WHISPER_MODEL_FILENAME),
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
        exit();
      }, 1500);
    } catch (err) {
      const code = (err as NodeJS.ErrnoException).code;
      const message =
        code === 'EACCES' || code === 'EPERM'
          ? `Permission denied writing to ${configManager.homePath}. Check that you have write access to that directory.`
          : err instanceof Error
            ? err.message
            : String(err);
      setState((s) => ({ ...s, errorMessage: message }));
      setStep('error');
    }
  }

  return (
    <Box flexDirection="column" paddingY={1}>
      <Box marginBottom={1}>
        <Text bold color="cyan">
          Ten-Second Tom — Setup Wizard
        </Text>
      </Box>

      {step === 'llm-provider' && (
        <Box flexDirection="column">
          <Text>Step 1 of 4: Choose your LLM provider</Text>
          <Box marginTop={1}>
            <SelectInput items={llmProviderItems} onSelect={handleLlmProviderSelect} />
          </Box>
        </Box>
      )}

      {step === 'llm-cloud-key' && (
        <Box flexDirection="column">
          <Text>Step 2 of 4: Enter your Anthropic API key</Text>
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
        </Box>
      )}

      {step === 'llm-local-endpoint' && (
        <Box flexDirection="column">
          <Text>Step 2 of 4: Local LLM endpoint URL</Text>
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
        </Box>
      )}

      {step === 'llm-local-model-loading' && (
        <Box flexDirection="column">
          <Text>Step 2 of 4: Choose a local model</Text>
          <Box marginTop={1}>
            <Text dimColor>Querying Ollama for installed models...</Text>
          </Box>
        </Box>
      )}

      {step === 'llm-local-model' && (
        <Box flexDirection="column">
          <Text>Step 2 of 4: Choose a local model</Text>
          {ollamaStatusMessage.length > 0 && (
            <Box marginTop={1}>
              <Text color="yellow">{ollamaStatusMessage}</Text>
            </Box>
          )}
          <Box marginTop={1}>
            <SelectInput items={ollamaModelItems} onSelect={handleLocalModelSelect} />
          </Box>
        </Box>
      )}

      {step === 'embedding-provider' && (
        <Box flexDirection="column">
          <Text>Step 3 of 4: Choose your embedding provider</Text>
          <Box marginTop={1}>
            <Text dimColor>Embeddings enable semantic (meaning-based) search</Text>
          </Box>
          <Box marginTop={1}>
            <SelectInput items={embeddingProviderItems} onSelect={handleEmbeddingProviderSelect} />
          </Box>
        </Box>
      )}

      {step === 'stt-info' && (
        <Box flexDirection="column">
          <Text>Step 4 of 4: Speech-to-Text model</Text>
          <Box marginTop={1} flexDirection="column">
            <Text>
              Tom uses{' '}
              <Text bold color="yellow">
                Whisper
              </Text>{' '}
              for local, private transcription.
            </Text>
            <Text>
              Model: <Text bold>ggml-distil-small.en</Text> <Text dimColor>(~380 MB)</Text>
            </Text>
            <Text dimColor>The model will be downloaded next.</Text>
          </Box>
          <Box marginTop={1}>
            <SelectInput
              items={[{ label: 'Continue and download model', value: 'continue' }]}
              onSelect={handleSttContinue}
            />
          </Box>
        </Box>
      )}

      {step === 'model-download' && (
        <Box flexDirection="column">
          <Text>Step 4 of 4: Download Whisper Model</Text>
          <Box marginTop={1} flexDirection="column">
            {downloadProgress.status === 'checking' && (
              <Text dimColor>Checking for existing model...</Text>
            )}
            {downloadProgress.status === 'already-downloaded' && (
              <Text color="green">Model already downloaded. Continuing...</Text>
            )}
            {downloadProgress.status === 'downloading' && (
              <>
                <Text>Downloading ggml-distil-small.en (~380 MB)...</Text>
                <Text>
                  {'  '}
                  {downloadProgress.totalBytes > 0
                    ? `${makeProgressBar(
                        (downloadProgress.bytesDownloaded / downloadProgress.totalBytes) * 100,
                      )} ${Math.round(
                        (downloadProgress.bytesDownloaded / downloadProgress.totalBytes) * 100,
                      )}% (${formatBytes(downloadProgress.bytesDownloaded)} / ${formatBytes(
                        downloadProgress.totalBytes,
                      )})`
                    : `Downloaded ${formatBytes(downloadProgress.bytesDownloaded)}...`}
                </Text>
              </>
            )}
            {downloadProgress.status === 'complete' && (
              <Text color="green">Download complete! Saving configuration...</Text>
            )}
            {downloadProgress.status === 'error' && (
              <Box flexDirection="column">
                <Text color="red">{downloadProgress.errorMessage}</Text>
                <Box marginTop={1}>
                  <SelectInput
                    items={[
                      { label: 'Retry download', value: 'retry' },
                      { label: 'Skip (download later)', value: 'skip' },
                    ]}
                    onSelect={handleDownloadErrorChoice}
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
            <Text dimColor>Press Enter or q to exit.</Text>
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
