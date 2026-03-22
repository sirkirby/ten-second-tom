import { describe, it, expect, vi } from 'vitest';
import type { AgentConfig } from '../config.js';

// Mock the Anthropic SDK before importing TomAgent
const mockMessagesCreate = vi.fn().mockResolvedValue({
  content: [
    {
      type: 'text',
      text: JSON.stringify({
        sentiment: { score: 0.7, label: 'positive — excited about progress', confidence: 0.92 },
        summary: 'Positive update about shipping the new dashboard',
        topics: ['dashboard', 'shipping', 'progress'],
        emotions: ['excitement', 'satisfaction'],
      }),
    },
  ],
});

vi.mock('@anthropic-ai/sdk', () => {
  return {
    default: vi.fn().mockImplementation(() => ({
      messages: { create: mockMessagesCreate },
    })),
  };
});

describe('TomAgent', () => {
  const cloudConfig: AgentConfig = {
    provider: 'cloud',
    apiKey: 'sk-ant-test-key',
  };

  it('analyzes text and returns structured analysis', async () => {
    const { TomAgent } = await import('../tom-agent.js');
    const agent = new TomAgent(cloudConfig);

    const result = await agent.analyze('We shipped the new dashboard today. Really excited about the progress!');

    expect(result.sentiment.score).toBe(0.7);
    expect(result.sentiment.label).toBe('positive — excited about progress');
    expect(result.sentiment.confidence).toBe(0.92);
    expect(result.summary).toBe('Positive update about shipping the new dashboard');
    expect(result.raw).toMatchObject({
      topics: ['dashboard', 'shipping', 'progress'],
      emotions: ['excitement', 'satisfaction'],
    });
  });

  it('handles empty content gracefully', async () => {
    const { TomAgent } = await import('../tom-agent.js');
    const agent = new TomAgent(cloudConfig);

    await expect(agent.analyze('')).rejects.toThrow('Content cannot be empty');
  });

  it('handles whitespace-only content gracefully', async () => {
    const { TomAgent } = await import('../tom-agent.js');
    const agent = new TomAgent(cloudConfig);

    await expect(agent.analyze('   ')).rejects.toThrow('Content cannot be empty');
  });

  it('throws descriptive error when LLM returns malformed JSON', async () => {
    mockMessagesCreate.mockResolvedValueOnce({
      content: [{ type: 'text', text: 'This is not valid JSON at all' }],
    });

    const { TomAgent } = await import('../tom-agent.js');
    const agent = new TomAgent(cloudConfig);

    await expect(agent.analyze('some content')).rejects.toThrow(
      'Failed to parse analysis response from LLM',
    );
  });

  it('throws descriptive error when LLM response is missing sentiment', async () => {
    mockMessagesCreate.mockResolvedValueOnce({
      content: [
        {
          type: 'text',
          text: JSON.stringify({ summary: 'No sentiment here', topics: [] }),
        },
      ],
    });

    const { TomAgent } = await import('../tom-agent.js');
    const agent = new TomAgent(cloudConfig);

    await expect(agent.analyze('some content')).rejects.toThrow(
      'Failed to parse analysis response from LLM',
    );
  });
});
