import React, { useState, useEffect, useRef } from 'react';
import { Box, Text } from 'ink';
import Spinner from 'ink-spinner';
import { join } from 'node:path';
import { buildServicesFromConfig } from '@ten-second-tom/core';
import type { EntryAnalysis, ServiceContainer } from '@ten-second-tom/core';
import { TranscriptBox } from '../components/TranscriptBox.js';
import { ErrorDisplay } from '../components/ErrorDisplay.js';
import { checkSetupComplete } from '../hooks/useSetupGuard.js';
import { useAutoExit } from '../hooks/useAutoExit.js';
import { runAnalysisPipeline } from '../commands/record.js';
import type { AppContext } from '../commands/registry.js';
import type { ResultsSummaryProps } from '../components/ResultsSummary.js';
import { AUTO_EXIT_DELAY_MS } from '../constants.js';

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

export interface ProcessingScreenProps {
  context: AppContext;
  // For post-recording:
  audioRelPath?: string;
  liveTranscript?: string;
  duration?: number;
  // For re-analysis:
  entryId?: string;
  // Callback when done
  onComplete: (result: ResultsSummaryProps) => void;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

type Phase = 'transcribing' | 'analyzing' | 'loading' | 'done' | 'error';

// ---------------------------------------------------------------------------
// ProcessingScreen
// ---------------------------------------------------------------------------

export function ProcessingScreen({
  context,
  audioRelPath,
  liveTranscript,
  duration,
  entryId,
  onComplete,
}: ProcessingScreenProps) {
  const [phase, setPhase] = useState<Phase>(entryId ? 'loading' : 'transcribing');
  const [transcript, setTranscript] = useState(liveTranscript ?? '');
  const [error, setError] = useState<string | null>(null);

  // Guard against StrictMode double-execution
  const started = useRef(false);

  // -------------------------------------------------------------------------
  // Post-recording pipeline
  // -------------------------------------------------------------------------
  useEffect(() => {
    if (entryId) return; // handled by re-analysis effect
    if (started.current) return;
    started.current = true;

    let cancelled = false;

    async function run() {
      try {
        // Resolve services — prefer context services, fall back to fresh build
        let svcs: ServiceContainer;
        if (context.services) {
          svcs = context.services;
        } else {
          const guard = checkSetupComplete();
          if (!guard.ok) {
            if (!cancelled) {
              setError(guard.error);
              setPhase('error');
            }
            return;
          }
          svcs = buildServicesFromConfig(guard.config, guard.configManager);
        }

        // Phase 1: Transcribe the audio file
        let finalTranscript = '';

        if (audioRelPath && context.configManager) {
          try {
            const fullAudioPath = join(context.configManager.audioPath, audioRelPath);

            // Load STT model if needed
            if (!svcs.transcription.isModelLoaded()) {
              const guard = checkSetupComplete();
              if (guard.ok) {
                await svcs.transcription.loadModel(guard.config.stt.modelPath);
              }
            }

            finalTranscript = await svcs.transcription.transcribeFile(fullAudioPath);
            if (!cancelled) {
              setTranscript(finalTranscript);
            }
          } catch {
            // Transcription failed — fall back to live transcript if available
            finalTranscript = liveTranscript ?? '';
          }
        } else {
          finalTranscript = liveTranscript ?? '';
        }

        if (cancelled) return;

        // Phase 2: Analyse + save
        setPhase('analyzing');

        const result = await runAnalysisPipeline(finalTranscript, audioRelPath, svcs);

        if (cancelled) return;

        setPhase('done');
        onComplete({
          duration,
          transcript: finalTranscript,
          analysis: result.analysis,
          warnings: result.warnings,
          entryType: 'recording',
        });
      } catch (err) {
        if (!cancelled) {
          const msg = err instanceof Error ? err.message : String(err);
          setError(`Processing failed: ${msg}`);
          setPhase('error');
        }
      }
    }

    void run();

    return () => {
      cancelled = true;
    };
  }, [entryId]);

  // -------------------------------------------------------------------------
  // Re-analysis pipeline
  // -------------------------------------------------------------------------
  useEffect(() => {
    if (!entryId) return; // handled by post-recording effect
    if (started.current) return;
    started.current = true;

    let cancelled = false;

    async function run() {
      try {
        // Resolve services
        let svcs: ServiceContainer;
        if (context.services) {
          svcs = context.services;
        } else {
          const guard = checkSetupComplete();
          if (!guard.ok) {
            if (!cancelled) {
              setError(guard.error);
              setPhase('error');
            }
            return;
          }
          svcs = buildServicesFromConfig(guard.config, guard.configManager);
        }

        // Phase 1: Load the entry
        const entry = await svcs.storage.getEntry(entryId);
        if (!entry) {
          if (!cancelled) {
            setError(`Entry not found: ${entryId}`);
            setPhase('error');
          }
          return;
        }

        if (!cancelled) {
          setTranscript(entry.content);
          setPhase('analyzing');
        }

        // Phase 2: Re-run analysis
        const [analysisResult, embeddingResult] = await Promise.allSettled([
          svcs.agent.analyze(entry.content),
          svcs.embedding.embed(entry.content),
        ]);

        if (cancelled) return;

        const warnings: string[] = [];
        let analysis: EntryAnalysis | null = null;

        if (analysisResult.status === 'fulfilled') {
          analysis = analysisResult.value;
          await svcs.storage.updateEntryAnalysis(entry.id, analysis);
        } else {
          const msg =
            analysisResult.reason instanceof Error
              ? analysisResult.reason.message
              : String(analysisResult.reason);
          if (!cancelled) {
            setError(`AI analysis failed: ${msg}`);
            setPhase('error');
          }
          return;
        }

        if (embeddingResult.status === 'fulfilled') {
          try {
            await svcs.storage.updateEntryEmbedding(entry.id, embeddingResult.value);
          } catch {
            warnings.push('Embedding storage unavailable — entry saved without vector index.');
          }
        } else {
          warnings.push('Embedding unavailable — entry saved without vector index.');
        }

        if (cancelled) return;

        setPhase('done');
        onComplete({
          transcript: entry.content,
          analysis,
          warnings,
          entryType: entry.type,
        });
      } catch (err) {
        if (!cancelled) {
          const msg = err instanceof Error ? err.message : String(err);
          setError(`Re-analysis failed: ${msg}`);
          setPhase('error');
        }
      }
    }

    void run();

    return () => {
      cancelled = true;
    };
  }, [entryId]);

  // -------------------------------------------------------------------------
  // One-shot: auto-exit after showing an error
  // -------------------------------------------------------------------------
  useAutoExit(phase === 'error', AUTO_EXIT_DELAY_MS, context.oneShot);

  // -------------------------------------------------------------------------
  // Render
  // -------------------------------------------------------------------------
  if (phase === 'error') {
    return <ErrorDisplay message={error ?? 'Unknown error'} />;
  }

  if (phase === 'loading') {
    return (
      <Box paddingY={1}>
        <Text color="cyan">
          <Spinner type="dots" />
        </Text>
        <Text> Loading entry...</Text>
      </Box>
    );
  }

  if (phase === 'transcribing') {
    return (
      <Box flexDirection="column" gap={1}>
        <Box>
          <Text color="cyan">
            <Spinner type="dots" />
          </Text>
          <Text> Transcribing...</Text>
        </Box>

        {/* Show live transcript preview while batch transcription runs */}
        {transcript.length > 0 && (
          <Box paddingLeft={2} flexDirection="column">
            <Text dimColor italic>
              Live preview
            </Text>
            <TranscriptBox text={transcript} />
          </Box>
        )}
      </Box>
    );
  }

  if (phase === 'analyzing') {
    return (
      <Box flexDirection="column" gap={1}>
        {/* Show the final transcript while analysis runs */}
        {transcript.length > 0 && (
          <Box paddingLeft={2}>
            <TranscriptBox text={transcript} />
          </Box>
        )}

        <Box>
          <Text color="cyan">
            <Spinner type="dots" />
          </Text>
          <Text> Analysing...</Text>
        </Box>
      </Box>
    );
  }

  // phase === 'done' — parent will replace us with ResultsSummary in Static history
  return null;
}
