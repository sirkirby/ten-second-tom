import Anthropic from '@anthropic-ai/sdk';
import type { EntryAnalysis } from '../types/entry.js';
import { type AgentConfig, getModelId, getBaseUrl } from './config.js';

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
  private readonly config: AgentConfig;

  constructor(config: AgentConfig) {
    this.config = config;
    this.client = new Anthropic({
      apiKey: config.apiKey,
      baseURL: getBaseUrl(config),
    });
  }

  async analyze(content: string): Promise<EntryAnalysis> {
    if (!content || content.trim().length === 0) {
      throw new Error('Content cannot be empty');
    }

    const modelId = getModelId(this.config);

    const response = await this.client.messages.create({
      model: modelId,
      max_tokens: 1024,
      messages: [
        {
          role: 'user',
          content: `${ANALYSIS_PROMPT}\n\nText to analyze:\n${content}`,
        },
      ],
    });

    const textBlock = response.content.find((block) => block.type === 'text');
    if (!textBlock || textBlock.type !== 'text') {
      throw new Error('No text response from model');
    }

    const parsed = JSON.parse(textBlock.text) as Record<string, unknown>;

    const sentimentRaw = parsed['sentiment'] as Record<string, unknown>;
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
