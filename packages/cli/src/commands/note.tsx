import React, { useState, useEffect } from 'react';
import { Box, Text, useApp } from 'ink';
import TextInput from 'ink-text-input';
import { Command } from 'commander';
import { render } from 'ink';
import { ConfigManager } from '@ten-second-tom/core';
import type { EntryAnalysis } from '@ten-second-tom/core';
import { SentimentDisplay } from '../components/SentimentDisplay.js';
import { buildServicesFromConfig, runAnalysisPipeline } from './record.js';

// ---------------------------------------------------------------------------
// Pipeline orchestration (extracted for testability)
// ---------------------------------------------------------------------------

export interface NotePipelineResult {
  entryId: string | null;
  analysis: EntryAnalysis | null;
  warnings: string[];
  error: string | null;
}

/**
 * Orchestrate the note pipeline: validate input, check setup, run analysis.
 * Exported for testing.
 */
export async function runNotePipeline(text: string): Promise<NotePipelineResult> {
  const empty: NotePipelineResult = {
    entryId: null,
    analysis: null,
    warnings: [],
    error: null,
  };

  if (!text.trim()) {
    return { ...empty, error: 'Note text is empty — nothing to save.' };
  }

  const configManager = new ConfigManager();

  if (!configManager.isSetupComplete()) {
    return {
      ...empty,
      error: 'Tom is not configured. Run `tom setup` first.',
    };
  }

  const config = configManager.load()!;
  const services = buildServicesFromConfig(config, configManager);

  try {
    const result = await runAnalysisPipeline(text.trim(), undefined, services, {
      entryType: 'note',
      inputMethod: 'typed',
    });

    return {
      entryId: result.entryId,
      analysis: result.analysis,
      warnings: result.warnings,
      error: null,
    };
  } finally {
    services.storage.close();
  }
}

// ---------------------------------------------------------------------------
// React component
// ---------------------------------------------------------------------------

type Phase = 'input' | 'analyzing' | 'done' | 'error';

function NoteCommand() {
  const { exit } = useApp();

  const [phase, setPhase] = useState<Phase>('input');
  const [noteText, setNoteText] = useState('');
  const [analysis, setAnalysis] = useState<EntryAnalysis | null>(null);
  const [warnings, setWarnings] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);

  // -------------------------------------------------------------------------
  // On mount: check setup guard
  // -------------------------------------------------------------------------
  useEffect(() => {
    const configManager = new ConfigManager();
    if (!configManager.isSetupComplete()) {
      setError('Tom is not configured. Run `tom setup` first.');
      setPhase('error');
    }
  }, []);

  // -------------------------------------------------------------------------
  // Auto-exit after done / error
  // -------------------------------------------------------------------------
  useEffect(() => {
    if (phase === 'done' || phase === 'error') {
      const timer = setTimeout(() => exit(), 5000);
      return () => clearTimeout(timer);
    }
    return undefined;
  }, [phase, exit]);

  // -------------------------------------------------------------------------
  // Submit handler
  // -------------------------------------------------------------------------
  async function handleSubmit(text: string) {
    if (!text.trim()) return; // ignore empty

    setPhase('analyzing');

    try {
      const configManager = new ConfigManager();
      const config = configManager.load()!;
      const services = buildServicesFromConfig(config, configManager);

      const result = await runAnalysisPipeline(text.trim(), undefined, services, {
        entryType: 'note',
        inputMethod: 'typed',
      });

      services.storage.close();

      setAnalysis(result.analysis);
      setWarnings(result.warnings);
      setPhase('done');
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      setError(`Failed to save note: ${msg}`);
      setPhase('error');
    }
  }

  // -------------------------------------------------------------------------
  // Render
  // -------------------------------------------------------------------------
  if (phase === 'error') {
    return (
      <Box flexDirection="column" paddingY={1}>
        <Text color="red" bold>
          Error
        </Text>
        <Text color="red">{error}</Text>
      </Box>
    );
  }

  if (phase === 'input') {
    return (
      <Box flexDirection="column" paddingY={1}>
        <Text bold>{'📝 New Note'}</Text>
        <Text>Type your note and press Enter:</Text>
        <TextInput
          value={noteText}
          onChange={setNoteText}
          onSubmit={(value) => void handleSubmit(value)}
        />
      </Box>
    );
  }

  if (phase === 'analyzing') {
    return (
      <Box paddingY={1}>
        <Text color="cyan">Analysing note...</Text>
      </Box>
    );
  }

  // phase === 'done'
  return (
    <Box flexDirection="column" paddingY={1}>
      <Text color="green" bold>
        {'✓ Note saved'}
      </Text>

      {analysis !== null && (
        <Box marginTop={1}>
          <SentimentDisplay analysis={analysis} />
        </Box>
      )}

      {warnings.length > 0 && (
        <Box marginTop={1} flexDirection="column">
          {warnings.map((w, i) => (
            <Text key={i} color="yellow">
              {'⚠ '}{w}
            </Text>
          ))}
        </Box>
      )}

      <Box marginTop={1}>
        <Text dimColor>Press Enter or q to exit.</Text>
      </Box>
    </Box>
  );
}

// ---------------------------------------------------------------------------
// Commander command registration
// ---------------------------------------------------------------------------

export const noteCommand = new Command('note')
  .description('Create a text note (type or dictate)')
  .action(() => {
    render(<NoteCommand />);
  });
