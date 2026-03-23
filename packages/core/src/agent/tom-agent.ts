import Anthropic from '@anthropic-ai/sdk';
import type { EntryAnalysis } from '../types/entry.js';
import type { LlmConfig } from '../types/config.js';
import { getModelId, getBaseUrl } from './config.js';
import { ANALYSIS_MAX_TOKENS, DEFAULT_OLLAMA_ENDPOINT } from '../constants.js';

export interface IAgentService {
  analyze(content: string): Promise<EntryAnalysis>;
}

const ANALYSIS_PROMPT = `You are an AI assistant that analyzes journal entries and notes.
Analyze the provided text and return ONLY a JSON object with this exact structure:
{
  "sentiment": { "score": <number from -1 to 1>, "label": "<descriptive label>", "confidence": <number from 0 to 1> },
  "summary": "<one sentence summary>",
  "topics": ["<topic1>", "<topic2>"],
  "emotions": ["<emotion1>", "<emotion2>"]
}

Do not include any text outside the JSON object.`;

/**
 * Parses the raw JSON text from the LLM into a structured EntryAnalysis.
 * Shared between cloud and local analysis paths.
 */
function parseAnalysisResponse(text: string): EntryAnalysis {
  let parsed: Record<string, unknown>;
  try {
    parsed = JSON.parse(text) as Record<string, unknown>;
  } catch {
    throw new Error('Failed to parse analysis response from LLM');
  }

  const sentimentRaw = parsed['sentiment'] as Record<string, unknown> | undefined;
  if (!sentimentRaw || typeof sentimentRaw !== 'object') {
    throw new Error('Failed to parse analysis response from LLM');
  }

  const score = Math.max(-1, Math.min(1, Number(sentimentRaw['score'])));
  const confidence = Math.max(0, Math.min(1, Number(sentimentRaw['confidence'])));

  return {
    sentiment: {
      score,
      label: String(sentimentRaw['label']),
      confidence,
    },
    summary: String(parsed['summary']),
    raw: parsed,
  };
}

export class TomAgent implements IAgentService {
  private readonly config: LlmConfig;
  private readonly client: Anthropic | null;
  private readonly modelId: string;
  private readonly baseUrl: string | undefined;

  constructor(config: LlmConfig) {
    this.config = config;
    this.modelId = getModelId(config);
    this.baseUrl = getBaseUrl(config);

    if (config.provider === 'cloud') {
      this.client = new Anthropic({ apiKey: config.apiKey });
    } else {
      // Local providers use Ollama's native API — no Anthropic SDK needed
      this.client = null;
    }
  }

  async analyze(content: string): Promise<EntryAnalysis> {
    if (!content || content.trim().length === 0) {
      throw new Error('Content cannot be empty');
    }

    if (this.config.provider === 'local') {
      return this.analyzeLocal(content);
    }

    return this.analyzeCloud(content);
  }

  /**
   * Analyze via the Anthropic cloud API using the Anthropic SDK.
   */
  private async analyzeCloud(content: string): Promise<EntryAnalysis> {
    if (!this.client) {
      throw new Error('Anthropic client not initialised for cloud provider');
    }

    const response = await this.client.messages.create({
      model: this.modelId,
      max_tokens: ANALYSIS_MAX_TOKENS,
      system: ANALYSIS_PROMPT,
      messages: [
        {
          role: 'user',
          content,
        },
      ],
    });

    const textBlock = response.content.find(
      (block): block is Anthropic.TextBlock => block.type === 'text',
    );
    if (!textBlock) {
      throw new Error('No text response from model');
    }

    return parseAnalysisResponse(textBlock.text);
  }

  /**
   * Analyze via Ollama's native /api/chat endpoint.
   * The Anthropic SDK cannot talk to Ollama — Ollama exposes an
   * OpenAI-compatible API, not an Anthropic-compatible one.
   */
  private async analyzeLocal(content: string): Promise<EntryAnalysis> {
    const endpoint = this.baseUrl ?? DEFAULT_OLLAMA_ENDPOINT;

    const response = await fetch(`${endpoint}/api/chat`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        model: this.modelId,
        messages: [
          { role: 'system', content: ANALYSIS_PROMPT },
          { role: 'user', content },
        ],
        stream: false,
        format: 'json',
      }),
    });

    if (!response.ok) {
      throw new Error(`Ollama error: ${response.status} ${response.statusText}`);
    }

    const data = (await response.json()) as { message?: { content?: string } };
    const text = data?.message?.content;
    if (!text) {
      throw new Error('No text response from model');
    }

    return parseAnalysisResponse(text);
  }
}
