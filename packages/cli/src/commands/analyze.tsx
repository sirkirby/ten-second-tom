import React, { useState, useEffect } from 'react';
import { Box, Text, useApp, useInput } from 'ink';
import { Command } from 'commander';
import { render } from 'ink';
import { ConfigManager } from '@ten-second-tom/core';
import type { EntryAnalysis } from '@ten-second-tom/core';
import { SentimentDisplay } from '../components/SentimentDisplay.js';
import { ErrorDisplay } from '../components/ErrorDisplay.js';
import { useAutoExit } from '../hooks/useAutoExit.js';
import { checkSetupComplete } from '../hooks/useSetupGuard.js';
import { buildServicesFromConfig } from './record.js';

// ---------------------------------------------------------------------------
// Pipeline types & orchestration (extracted for testability)
// ---------------------------------------------------------------------------

export interface AnalyzePipelineResult {
  entryId: string | null;
  analysis: EntryAnalysis | null;
  embeddingStored: boolean;
  error: string | null;
}

/**
 * Re-run AI analysis on an existing entry.
 * Unlike the recording/note pipeline this is an explicit user action so LLM
 * failures are surfaced as errors rather than degraded gracefully.
 * Exported for testing.
 *
 * @param entryId - The ID of the entry to re-analyse.
 */
export async function runAnalyzePipeline(entryId: string): Promise<AnalyzePipelineResult> {
  const empty: AnalyzePipelineResult = {
    entryId: null,
    analysis: null,
    embeddingStored: false,
    error: null,
  };

  const guard = checkSetupComplete();
  if (!guard.ok) {
    return { ...empty, error: guard.error };
  }

  const { config, configManager } = guard;
  const services = buildServicesFromConfig(config, configManager);

  try {
    const entry = await services.storage.getEntry(entryId);

    if (entry === undefined) {
      return { ...empty, error: `Entry not found: ${entryId}` };
    }

    // Run analysis + embedding in parallel.
    // Analysis is required (explicit user action), embedding is best-effort.
    const [analysisResult, embeddingResult] = await Promise.allSettled([
      services.agent.analyze(entry.content),
      services.embedding.embed(entry.content),
    ]);

    // Analysis failure is an error — this is an explicit re-analysis, not silent degradation.
    if (analysisResult.status === 'rejected') {
      const msg =
        analysisResult.reason instanceof Error
          ? analysisResult.reason.message
          : String(analysisResult.reason);
      return {
        ...empty,
        entryId: entry.id,
        error: `AI analysis failed: ${msg}`,
      };
    }

    const analysis = analysisResult.value;
    await services.storage.updateEntryAnalysis(entry.id, analysis);

    // Embedding is best-effort — failure does not block the analysis result.
    let embeddingStored = false;
    if (embeddingResult.status === 'fulfilled') {
      try {
        await services.storage.updateEntryEmbedding(entry.id, embeddingResult.value);
        embeddingStored = true;
      } catch {
        // Non-fatal — entry analysis already saved above.
      }
    }

    return {
      entryId: entry.id,
      analysis,
      embeddingStored,
      error: null,
    };
  } finally {
    services.storage.close();
  }
}

// ---------------------------------------------------------------------------
// React component
// ---------------------------------------------------------------------------

type Phase = 'init' | 'analyzing' | 'done' | 'error';

function AnalyzeCommand({ entryId }: { entryId: string }) {
  const { exit } = useApp();

  const [phase, setPhase] = useState<Phase>('init');
  const [analysis, setAnalysis] = useState<EntryAnalysis | null>(null);
  const [error, setError] = useState<string | null>(null);

  // -------------------------------------------------------------------------
  // On mount: run the pipeline
  // -------------------------------------------------------------------------
  useEffect(() => {
    async function run() {
      try {
        const result = await runAnalyzePipeline(entryId);

        if (result.error !== null) {
          setError(result.error);
          setPhase('error');
          return;
        }

        setAnalysis(result.analysis);
        setPhase('done');
      } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        setError(`Unexpected error: ${msg}`);
        setPhase('error');
      }
    }

    void run();
  }, [entryId]);

  // -------------------------------------------------------------------------
  // Auto-exit after done / error
  // -------------------------------------------------------------------------
  useAutoExit(phase === 'done' || phase === 'error');

  // -------------------------------------------------------------------------
  // Keyboard: allow Enter or q to exit after done / error
  // -------------------------------------------------------------------------
  useInput((input, key) => {
    if ((input === 'q' || key.return) && (phase === 'done' || phase === 'error')) {
      exit();
    }
  });

  // -------------------------------------------------------------------------
  // Render
  // -------------------------------------------------------------------------
  if (phase === 'error') {
    return <ErrorDisplay message={error ?? 'Unknown error'} />;
  }

  if (phase === 'init' || phase === 'analyzing') {
    return (
      <Box paddingY={1}>
        <Text color="cyan">Analysing entry...</Text>
      </Box>
    );
  }

  // phase === 'done'
  return (
    <Box flexDirection="column" paddingY={1}>
      <Text color="green" bold>
        Analysis complete
      </Text>

      {analysis !== null && (
        <Box marginTop={1}>
          <SentimentDisplay analysis={analysis} />
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

export const analyzeCommand = new Command('analyze')
  .description('Re-run AI analysis on an entry')
  .argument('<entry-id>', 'ID of the entry to analyze')
  .action((entryId: string) => {
    render(<AnalyzeCommand entryId={entryId} />);
  });
