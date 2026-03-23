import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { LlmConfig } from '../../types/config.js';

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

// Mock global fetch for Ollama local tests
const mockFetch = vi.fn();

beforeEach(() => {
  vi.clearAllMocks();
  // Re-setup default mock for Anthropic
  mockMessagesCreate.mockResolvedValue({
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

  // -----------------------------------------------------------------------
  // Local provider tests (Ollama native API)
  // -----------------------------------------------------------------------

  describe('local provider', () => {
    const ollamaResponse = {
      message: {
        content: JSON.stringify({
          sentiment: { score: 0.5, label: 'neutral', confidence: 0.8 },
          summary: 'A routine daily update',
          topics: ['work'],
          emotions: ['calm'],
        }),
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
      expect(result.sentiment.label).toBe('neutral');
      expect(result.sentiment.confidence).toBe(0.8);
      expect(result.summary).toBe('A routine daily update');
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
