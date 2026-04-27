import type { Entry, EntryAnalysis } from '../types/entry.js';
import type { ServiceContainer } from './service-factory.js';

export interface PipelineResult {
  entryId: string;
  transcript: string;
  audioPath: string | undefined;
  analysis: EntryAnalysis | null;
  embeddingStored: boolean;
  warnings: string[];
}

export interface PipelineOptions {
  entryType?: Entry['type'];
  inputMethod?: Entry['inputMethod'];
  audioPath?: string;
}

export interface ReanalysisResult {
  entry: Entry;
  analysis: EntryAnalysis | null;
  embeddingStored: boolean;
  warnings: string[];
}

export async function runAnalysisPipeline(
  transcript: string,
  audioPathOrOptions: string | undefined,
  services: ServiceContainer,
  options?: PipelineOptions,
): Promise<PipelineResult> {
  const warnings: string[] = [];

  const audioPath =
    typeof audioPathOrOptions === 'string' ? audioPathOrOptions : options?.audioPath;
  const entryType = options?.entryType ?? 'recording';
  const inputMethod = options?.inputMethod ?? 'recorded';

  const entry = await services.storage.saveEntry({
    type: entryType,
    content: transcript,
    audioPath,
    inputMethod,
  });

  const [analysisResult, embeddingResult] = await Promise.allSettled([
    services.agent.analyze(transcript),
    services.embedding.embed(transcript),
  ]);

  const { analysis } = await persistAnalysisResult(entry.id, analysisResult, services, warnings);
  const embeddingStored = await persistEmbeddingResult(
    entry.id,
    embeddingResult,
    services,
    warnings,
  );

  return {
    entryId: entry.id,
    transcript,
    audioPath,
    analysis,
    embeddingStored,
    warnings,
  };
}

export async function reanalyzeEntry(
  entryId: string,
  services: ServiceContainer,
): Promise<ReanalysisResult | undefined> {
  const entry = await services.storage.getEntry(entryId);
  if (!entry) return undefined;

  const warnings: string[] = [];
  const [analysisResult, embeddingResult] = await Promise.allSettled([
    services.agent.analyze(entry.content),
    services.embedding.embed(entry.content),
  ]);

  const { analysis } = await persistAnalysisResult(entry.id, analysisResult, services, warnings);
  const embeddingStored = await persistEmbeddingResult(
    entry.id,
    embeddingResult,
    services,
    warnings,
  );

  return {
    entry,
    analysis,
    embeddingStored,
    warnings,
  };
}

async function persistAnalysisResult(
  entryId: string,
  result: PromiseSettledResult<EntryAnalysis>,
  services: ServiceContainer,
  warnings: string[],
): Promise<{ analysis: EntryAnalysis | null }> {
  if (result.status === 'rejected') {
    warnings.push(
      `AI analysis unavailable — entry saved without analysis. ${failureReason(result.reason)}`,
    );
    return { analysis: null };
  }

  try {
    await services.storage.updateEntryAnalysis(entryId, result.value);
    return { analysis: result.value };
  } catch {
    warnings.push('Analysis storage unavailable — entry saved without persisted analysis.');
    return { analysis: result.value };
  }
}

async function persistEmbeddingResult(
  entryId: string,
  result: PromiseSettledResult<Float32Array>,
  services: ServiceContainer,
  warnings: string[],
): Promise<boolean> {
  if (result.status === 'rejected') {
    warnings.push(
      `Embedding unavailable — entry saved without vector index. ${failureReason(result.reason)}`,
    );
    return false;
  }

  try {
    await services.storage.updateEntryEmbedding(entryId, result.value);
    return true;
  } catch {
    warnings.push('Embedding storage unavailable — entry saved without vector index.');
    return false;
  }
}

function failureReason(reason: unknown): string {
  if (reason instanceof Error && reason.message.trim().length > 0) {
    return `Reason: ${reason.message}`;
  }
  const message = String(reason).trim();
  return message.length > 0 ? `Reason: ${message}` : 'No error detail was provided.';
}
