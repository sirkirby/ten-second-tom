import { describe, it, expect } from 'vitest';
import { EntrySchema, EntryAnalysisSchema } from '../entry.js';

describe('EntrySchema', () => {
  it('validates a valid recording entry', () => {
    const entry = {
      id: '550e8400-e29b-41d4-a716-446655440000',
      type: 'recording' as const,
      content: 'We shipped the new dashboard today',
      audioPath: '2026-04/2026-04-01-550e8400.wav',
      inputMethod: 'recorded' as const,
      createdAt: '2026-04-01T10:00:00.000Z',
      updatedAt: '2026-04-01T10:00:00.000Z',
    };
    const result = EntrySchema.safeParse(entry);
    expect(result.success).toBe(true);
  });

  it('validates a valid note entry', () => {
    const entry = {
      id: '550e8400-e29b-41d4-a716-446655440001',
      type: 'note' as const,
      content: 'Need to follow up on deploy pipeline',
      inputMethod: 'typed' as const,
      createdAt: '2026-04-01T10:00:00.000Z',
      updatedAt: '2026-04-01T10:00:00.000Z',
    };
    const result = EntrySchema.safeParse(entry);
    expect(result.success).toBe(true);
  });

  it('validates a dictated note entry', () => {
    const entry = {
      id: '550e8400-e29b-41d4-a716-446655440002',
      type: 'note' as const,
      content: 'Dictated note about standup',
      inputMethod: 'dictated' as const,
      createdAt: '2026-04-01T10:00:00.000Z',
      updatedAt: '2026-04-01T10:00:00.000Z',
    };
    const result = EntrySchema.safeParse(entry);
    expect(result.success).toBe(true);
  });

  it('rejects invalid entry type', () => {
    const entry = {
      id: '550e8400-e29b-41d4-a716-446655440000',
      type: 'invalid',
      content: 'test',
      inputMethod: 'typed',
      createdAt: '2026-04-01T10:00:00.000Z',
      updatedAt: '2026-04-01T10:00:00.000Z',
    };
    const result = EntrySchema.safeParse(entry);
    expect(result.success).toBe(false);
  });

  it('rejects empty content', () => {
    const entry = {
      id: '550e8400-e29b-41d4-a716-446655440000',
      type: 'note',
      content: '',
      inputMethod: 'typed',
      createdAt: '2026-04-01T10:00:00.000Z',
      updatedAt: '2026-04-01T10:00:00.000Z',
    };
    const result = EntrySchema.safeParse(entry);
    expect(result.success).toBe(false);
  });

  it('allows optional analysis and embedding', () => {
    const entry = {
      id: '550e8400-e29b-41d4-a716-446655440000',
      type: 'recording' as const,
      content: 'test content',
      inputMethod: 'recorded' as const,
      analysis: {
        sentiment: { score: 0.7, label: 'positive — excited about launch', confidence: 0.9 },
        summary: 'Positive update about dashboard launch',
        raw: { topics: ['dashboard', 'launch'] },
      },
      createdAt: '2026-04-01T10:00:00.000Z',
      updatedAt: '2026-04-01T10:00:00.000Z',
    };
    const result = EntrySchema.safeParse(entry);
    expect(result.success).toBe(true);
  });
});

describe('EntryAnalysisSchema', () => {
  it('validates a valid analysis', () => {
    const analysis = {
      sentiment: { score: -0.3, label: 'mildly frustrated', confidence: 0.85 },
      summary: 'Frustration with deploy pipeline reliability',
      raw: {},
    };
    const result = EntryAnalysisSchema.safeParse(analysis);
    expect(result.success).toBe(true);
  });

  it('rejects sentiment score out of range', () => {
    const analysis = {
      sentiment: { score: 1.5, label: 'positive', confidence: 0.9 },
      summary: 'test',
      raw: {},
    };
    const result = EntryAnalysisSchema.safeParse(analysis);
    expect(result.success).toBe(false);
  });
});
