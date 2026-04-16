import { z } from 'zod';

export const SentimentSchema = z.object({
  score: z.number().min(-1).max(1),
  label: z.string().min(1),
  confidence: z.number().min(0).max(1),
});

export const EntryAnalysisSchema = z.object({
  sentiment: SentimentSchema,
  summary: z.string(),
  raw: z.record(z.string(), z.unknown()),
});

export const EntrySchema = z.object({
  id: z.string().uuid(),
  type: z.enum(['recording', 'note']),
  content: z.string().min(1),
  audioPath: z.string().optional(),
  inputMethod: z.enum(['typed', 'dictated', 'recorded']),
  analysis: EntryAnalysisSchema.optional(),
  embedding: z.instanceof(Float32Array).optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
});

export type Entry = z.infer<typeof EntrySchema>;
export type EntryAnalysis = z.infer<typeof EntryAnalysisSchema>;
export type Sentiment = z.infer<typeof SentimentSchema>;

export const CreateEntrySchema = EntrySchema.omit({
  id: true,
  analysis: true,
  embedding: true,
  createdAt: true,
  updatedAt: true,
});

export type CreateEntry = z.infer<typeof CreateEntrySchema>;
