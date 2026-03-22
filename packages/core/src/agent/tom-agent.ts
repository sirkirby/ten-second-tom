import Anthropic from '@anthropic-ai/sdk';
import type { EntryAnalysis } from '../types/entry.js';
import type { LlmConfig } from '../types/config.js';
import { getModelId, getBaseUrl } from './config.js';

const ANALYSIS_PROMPT = `You are an AI assistant that analyzes journal entries and notes.
Analyze the provided text and return ONLY a JSON object with this exact structure:
{
  "sentiment": { "score": <number from -1 to 1>, "label": "<descriptive label>", "confidence": <number from 0 to 1> },
  "summary": "<one sentence summary>",
  "topics": ["<topic1>", "<topic2>"],
  "emotions": ["<emotion1>", "<emotion2>"]
}

Do not include any text outside the JSON object.`;

export class TomAgent {
  private readonly client: Anthropic;
  private readonly modelId: string;

  constructor(config: LlmConfig) {
    const apiKey = config.provider === 'cloud' ? config.apiKey : '';
    this.modelId = getModelId(config);
    this.client = new Anthropic({
      apiKey,
      baseURL: getBaseUrl(config),
    });
  }

  async analyze(content: string): Promise<EntryAnalysis> {
    if (!content || content.trim().length === 0) {
      throw new Error('Content cannot be empty');
    }

    const response = await this.client.messages.create({
      model: this.modelId,
      max_tokens: 1024,
      system: ANALYSIS_PROMPT,
      messages: [
        {
          role: 'user',
          content,
        },
      ],
    });

    const textBlock = response.content.find((block) => block.type === 'text');
    if (!textBlock || textBlock.type !== 'text') {
      throw new Error('No text response from model');
    }

    let parsed: Record<string, unknown>;
    try {
      parsed = JSON.parse(textBlock.text) as Record<string, unknown>;
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
}
