import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { LlmConfig } from '../../types/config.js';

// Rich mock response matching the new analysis prompt structure
const richMockResponse = {
  sentiment: {
    score: 0.7,
    label: 'excited and proud — significant milestone shipped',
    confidence: 0.92,
    emotions: [
      { name: 'excitement', intensity: 0.8 },
      { name: 'pride', intensity: 0.6 },
    ],
  },
  summary:
    'The team shipped the new dashboard today ahead of schedule. This matters because it unblocks the sales team for their upcoming demos.',
  decisions: [
    {
      decision: 'Ship the dashboard to production',
      context: 'Feature is ready and demos are upcoming',
    },
  ],
  actionItems: [{ item: 'Notify the sales team about the deployment', owner: null }],
  topics: ['deployment', 'dashboard', 'release'],
  contextType: 'update',
  quotes: ['Really excited about the progress!'],
};

// Mock the Anthropic SDK before importing TomAgent
const mockMessagesCreate = vi.fn().mockResolvedValue({
  content: [
    {
      type: 'text',
      text: JSON.stringify(richMockResponse),
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

// Mock global fetch for Ollama local tests
const mockFetch = vi.fn();

beforeEach(() => {
  vi.clearAllMocks();
  // Re-setup default mock for Anthropic
  mockMessagesCreate.mockResolvedValue({
    content: [
      {
        type: 'text',
        text: JSON.stringify(richMockResponse),
      },
    ],
  });
  vi.stubGlobal('fetch', mockFetch);
});

describe('TomAgent', () => {
  const cloudConfig: LlmConfig = {
    provider: 'cloud',
    apiKey: 'sk-ant-test-key',
  };

  const localConfig: LlmConfig = {
    provider: 'local',
    localEndpoint: 'http://localhost:11434',
    modelId: 'qwen2.5:7b',
  };

  // -----------------------------------------------------------------------
  // Cloud provider tests (Anthropic SDK)
  // -----------------------------------------------------------------------

  describe('cloud provider', () => {
    it('analyzes text and returns structured analysis', async () => {
      const { TomAgent } = await import('../tom-agent.js');
      const agent = new TomAgent(cloudConfig);

      const result = await agent.analyze(
        'We shipped the new dashboard today. Really excited about the progress!',
      );

      expect(result.sentiment.score).toBe(0.7);
      expect(result.sentiment.label).toBe('excited and proud — significant milestone shipped');
      expect(result.sentiment.confidence).toBe(0.92);
      expect(result.summary).toBe(
        'The team shipped the new dashboard today ahead of schedule. This matters because it unblocks the sales team for their upcoming demos.',
      );
    });

    it('stores the full rich response in raw', async () => {
      const { TomAgent } = await import('../tom-agent.js');
      const agent = new TomAgent(cloudConfig);

      const result = await agent.analyze(
        'We shipped the new dashboard today. Really excited about the progress!',
      );

      expect(result.raw).toMatchObject({
        topics: ['deployment', 'dashboard', 'release'],
        contextType: 'update',
        quotes: ['Really excited about the progress!'],
        decisions: [
          {
            decision: 'Ship the dashboard to production',
            context: 'Feature is ready and demos are upcoming',
          },
        ],
        actionItems: [{ item: 'Notify the sales team about the deployment', owner: null }],
      });
    });

    it('stores emotions array in raw.sentiment.emotions', async () => {
      const { TomAgent } = await import('../tom-agent.js');
      const agent = new TomAgent(cloudConfig);

      const result = await agent.analyze(
        'We shipped the new dashboard today. Really excited about the progress!',
      );

      const sentimentRaw = result.raw['sentiment'] as Record<string, unknown>;
      const emotions = sentimentRaw['emotions'] as Array<{ name: string; intensity: number }>;
      expect(Array.isArray(emotions)).toBe(true);
      expect(emotions).toHaveLength(2);
      expect(emotions[0]).toMatchObject({ name: 'excitement', intensity: 0.8 });
      expect(emotions[1]).toMatchObject({ name: 'pride', intensity: 0.6 });
    });

    it('handles missing optional fields gracefully (empty decisions and actionItems)', async () => {
      const minimalResponse = {
        sentiment: { score: 0.0, label: 'neutral', confidence: 0.5, emotions: [] },
        summary: 'A brief note.',
        decisions: [],
        actionItems: [],
        topics: [],
        contextType: 'other',
        quotes: [],
      };
      mockMessagesCreate.mockResolvedValueOnce({
        content: [{ type: 'text', text: JSON.stringify(minimalResponse) }],
      });

      const { TomAgent } = await import('../tom-agent.js');
      const agent = new TomAgent(cloudConfig);

      const result = await agent.analyze('ok');

      expect(result.sentiment.score).toBe(0.0);
      expect(result.summary).toBe('A brief note.');
      expect(result.raw['decisions']).toEqual([]);
      expect(result.raw['actionItems']).toEqual([]);
      expect(result.raw['topics']).toEqual([]);
    });

    it('handles missing summary field gracefully', async () => {
      const responseWithoutSummary = {
        sentiment: { score: 0.3, label: 'mildly positive', confidence: 0.6, emotions: [] },
        decisions: [],
        actionItems: [],
        topics: ['work'],
        contextType: 'other',
        quotes: [],
      };
      mockMessagesCreate.mockResolvedValueOnce({
        content: [{ type: 'text', text: JSON.stringify(responseWithoutSummary) }],
      });

      const { TomAgent } = await import('../tom-agent.js');
      const agent = new TomAgent(cloudConfig);

      const result = await agent.analyze('Some content');

      expect(result.summary).toBe('');
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

  // -----------------------------------------------------------------------
  // Local provider tests (Ollama native API)
  // -----------------------------------------------------------------------

  describe('local provider', () => {
    const richOllamaResponse = {
      sentiment: {
        score: 0.5,
        label: 'calm and focused — routine check-in',
        confidence: 0.8,
        emotions: [
          { name: 'calm', intensity: 0.7 },
          { name: 'focus', intensity: 0.5 },
        ],
      },
      summary: 'A routine daily update with no major issues.',
      decisions: [],
      actionItems: [{ item: 'Review PR before end of day', owner: null }],
      topics: ['work', 'code-review'],
      contextType: 'update',
      quotes: [],
    };

    const ollamaResponse = {
      message: {
        content: JSON.stringify(richOllamaResponse),
      },
    };

    it('calls Ollama /api/chat endpoint instead of Anthropic SDK', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve(ollamaResponse),
      });

      const { TomAgent } = await import('../tom-agent.js');
      const agent = new TomAgent(localConfig);

      await agent.analyze('Had a regular day at work.');

      // Verify fetch was called with Ollama's native API
      expect(mockFetch).toHaveBeenCalledOnce();
      expect(mockFetch).toHaveBeenCalledWith(
        'http://localhost:11434/api/chat',
        expect.objectContaining({
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
        }),
      );

      // Verify the request body structure
      const callBody = JSON.parse(mockFetch.mock.calls[0][1].body) as Record<string, unknown>;
      expect(callBody).toMatchObject({
        model: 'qwen2.5:7b',
        stream: false,
        format: 'json',
      });
      expect(callBody['messages']).toEqual([
        { role: 'system', content: expect.stringContaining('sentiment') },
        { role: 'user', content: 'Had a regular day at work.' },
      ]);

      // Anthropic SDK should NOT have been called
      expect(mockMessagesCreate).not.toHaveBeenCalled();
    });

    it('returns structured analysis from Ollama response', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve(ollamaResponse),
      });

      const { TomAgent } = await import('../tom-agent.js');
      const agent = new TomAgent(localConfig);

      const result = await agent.analyze('Had a regular day at work.');

      expect(result.sentiment.score).toBe(0.5);
      expect(result.sentiment.label).toBe('calm and focused — routine check-in');
      expect(result.sentiment.confidence).toBe(0.8);
      expect(result.summary).toBe('A routine daily update with no major issues.');
    });

    it('stores rich fields in raw for local provider', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve(ollamaResponse),
      });

      const { TomAgent } = await import('../tom-agent.js');
      const agent = new TomAgent(localConfig);

      const result = await agent.analyze('Had a regular day at work.');

      expect(result.raw['contextType']).toBe('update');
      expect(result.raw['topics']).toEqual(['work', 'code-review']);
      expect(result.raw['actionItems']).toEqual([
        { item: 'Review PR before end of day', owner: null },
      ]);
    });

    it('throws on Ollama HTTP error', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 500,
        statusText: 'Internal Server Error',
      });

      const { TomAgent } = await import('../tom-agent.js');
      const agent = new TomAgent(localConfig);

      await expect(agent.analyze('some content')).rejects.toThrow('Ollama error: 500');
    });

    it('throws when Ollama returns empty message', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: () => Promise.resolve({ message: {} }),
      });

      const { TomAgent } = await import('../tom-agent.js');
      const agent = new TomAgent(localConfig);

      await expect(agent.analyze('some content')).rejects.toThrow('No text response from model');
    });

    it('throws when Ollama returns malformed JSON content', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: () =>
          Promise.resolve({
            message: { content: 'not valid json' },
          }),
      });

      const { TomAgent } = await import('../tom-agent.js');
      const agent = new TomAgent(localConfig);

      await expect(agent.analyze('some content')).rejects.toThrow(
        'Failed to parse analysis response from LLM',
      );
    });

    it('handles empty content gracefully for local provider', async () => {
      const { TomAgent } = await import('../tom-agent.js');
      const agent = new TomAgent(localConfig);

      await expect(agent.analyze('')).rejects.toThrow('Content cannot be empty');
      expect(mockFetch).not.toHaveBeenCalled();
    });
  });
});
