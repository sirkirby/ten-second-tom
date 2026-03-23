import type { EntryAnalysis, ServiceContainer } from '@ten-second-tom/core';

// ---------------------------------------------------------------------------
// Pipeline types & orchestration (extracted from record.tsx for shared use)
// ---------------------------------------------------------------------------

/**
 * Re-export ServiceContainer as RecordingPipelineServices for backward
 * compatibility.
 */
export type { ServiceContainer as RecordingPipelineServices } from '@ten-second-tom/core';

export interface PipelineResult {
  entryId: string;
  transcript: string;
  audioPath: string | undefined;
  analysis: EntryAnalysis | null;
  embeddingStored: boolean;
  warnings: string[];
}

export interface PipelineOptions {
  entryType?: 'recording' | 'note';
  inputMethod?: 'recorded' | 'typed' | 'dictated';
  audioPath?: string;
}

/**
 * Run the post-recording/note analysis pipeline.
 * Returns the analysis result (or null) and any warnings.
 *
 * @param transcript - The text content to analyse.
 * @param audioPathOrOptions - For recordings: the audio file path (string).
 *   For notes: pass undefined or a PipelineOptions object.
 * @param services - The pipeline services.
 * @param options - Optional pipeline options (used when audioPathOrOptions is undefined).
 */
export async function runAnalysisPipeline(
  transcript: string,
  audioPathOrOptions: string | undefined,
  services: ServiceContainer,
  options?: PipelineOptions,
): Promise<PipelineResult> {
  const warnings: string[] = [];

  // Resolve audioPath and entry metadata from overloaded argument.
  const audioPath =
    typeof audioPathOrOptions === 'string' ? audioPathOrOptions : options?.audioPath;
  const entryType = options?.entryType ?? 'recording';
  const inputMethod = options?.inputMethod ?? 'recorded';

  // Save the entry first — capture always succeeds if the mic worked.
  const entry = await services.storage.saveEntry({
    type: entryType,
    content: transcript,
    audioPath,
    inputMethod,
  });

  // Run analysis + embedding in parallel, degrading gracefully on failure.
  const [analysisResult, embeddingResult] = await Promise.allSettled([
    services.agent.analyze(transcript),
    services.embedding.embed(transcript),
  ]);

  let analysis: EntryAnalysis | null = null;
  let embeddingStored = false;

  if (analysisResult.status === 'fulfilled') {
    analysis = analysisResult.value;
    await services.storage.updateEntryAnalysis(entry.id, analysis);
  } else {
    warnings.push(
      'AI analysis unavailable — entry saved without analysis. Check your LLM configuration.',
    );
  }

  if (embeddingResult.status === 'fulfilled') {
    try {
      await services.storage.updateEntryEmbedding(entry.id, embeddingResult.value);
      embeddingStored = true;
    } catch {
      // Vector storage not yet implemented — non-fatal.
      warnings.push('Embedding storage unavailable — entry saved without vector index.');
    }
  } else {
    warnings.push('Embedding unavailable — entry saved without vector index.');
  }

  return {
    entryId: entry.id,
    transcript,
    audioPath,
    analysis,
    embeddingStored,
    warnings,
  };
}
