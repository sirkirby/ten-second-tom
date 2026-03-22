import React, { useState } from 'react';
import { Box, Text, useApp, useInput } from 'ink';
import SelectInput from 'ink-select-input';
import TextInput from 'ink-text-input';
import { Command } from 'commander';
import { render } from 'ink';
import { join } from 'node:path';
import { ConfigManager } from '@ten-second-tom/core';
import type { AppConfig, LlmConfig, EmbeddingConfig } from '@ten-second-tom/core';
import { useAutoExit } from '../hooks/useAutoExit.js';

type Step =
  | 'llm-provider'
  | 'llm-cloud-key'
  | 'llm-local-endpoint'
  | 'llm-local-model'
  | 'embedding-provider'
  | 'stt-info'
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

const llmProviderItems = [
  { label: 'Cloud (Claude via Anthropic API)', value: 'cloud' as const },
  { label: 'Local (Ollama / LM Studio)', value: 'local' as const },
];

const localModelItems = [
  { label: 'qwen2.5:7b (recommended)', value: 'qwen2.5:7b' },
  { label: 'mistral:7b', value: 'mistral:7b' },
  { label: 'llama3.2:3b', value: 'llama3.2:3b' },
];

const embeddingProviderItems = [
  { label: 'Ollama (local vectors, recommended)', value: 'ollama' as const },
  { label: 'Cloud (Voyage AI)', value: 'cloud' as const },
  { label: 'None (keyword search only)', value: 'none' as const },
];

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
        errorMessage: 'Invalid API key format. Anthropic API keys start with "sk-ant-". Get your key at https://console.anthropic.com',
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
    setStep('llm-local-model');
  }

  function handleLocalModelSelect(item: { value: string }) {
    setState((s) => ({ ...s, localModelId: item.value }));
    setStep('embedding-provider');
  }

  function handleEmbeddingProviderSelect(item: {
    value: 'ollama' | 'cloud' | 'none';
  }) {
    setState((s) => ({ ...s, embeddingProvider: item.value }));
    setStep('stt-info');
  }

  function handleSttContinue(item: { value: string }) {
    if (item.value === 'continue') {
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
        modelPath: join(configManager.modelsPath, 'ggml-distil-small.en.bin'),
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
            <SelectInput
              items={llmProviderItems}
              onSelect={handleLlmProviderSelect}
            />
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
              onChange={(val) =>
                setState((s) => ({ ...s, localEndpoint: val }))
              }
              onSubmit={handleLocalEndpointSubmit}
              placeholder="http://localhost:11434"
            />
          </Box>
        </Box>
      )}

      {step === 'llm-local-model' && (
        <Box flexDirection="column">
          <Text>Step 2 of 4: Choose a local model</Text>
          <Box marginTop={1}>
            <SelectInput
              items={localModelItems}
              onSelect={handleLocalModelSelect}
            />
          </Box>
        </Box>
      )}

      {step === 'embedding-provider' && (
        <Box flexDirection="column">
          <Text>Step 3 of 4: Choose your embedding provider</Text>
          <Box marginTop={1}>
            <Text dimColor>
              Embeddings enable semantic (meaning-based) search
            </Text>
          </Box>
          <Box marginTop={1}>
            <SelectInput
              items={embeddingProviderItems}
              onSelect={handleEmbeddingProviderSelect}
            />
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
              Model:{' '}
              <Text bold>ggml-distil-small.en</Text>{' '}
              <Text dimColor>(~380MB)</Text>
            </Text>
            <Text dimColor>
              The model will be downloaded automatically on first recording.
            </Text>
          </Box>
          <Box marginTop={1}>
            <SelectInput
              items={[{ label: 'Continue and save configuration', value: 'continue' }]}
              onSelect={handleSttContinue}
            />
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
