import {
  EMBEDDING_AVAILABILITY_TIMEOUT_MS,
  EMBEDDING_AVAILABILITY_CACHE_MS,
} from '../constants.js';

export interface IEmbeddingService {
  embed(text: string): Promise<Float32Array>;
  isAvailable(): Promise<boolean>;
}

export interface OllamaEmbeddingConfig {
  model: string;
  endpoint: string;
}

export class OllamaEmbeddingService implements IEmbeddingService {
  private readonly model: string;
  private readonly endpoint: string;

  /** Cached availability result: [value, expiresAt] */
  private availabilityCache: [boolean, number] | null = null;

  constructor({ model, endpoint }: OllamaEmbeddingConfig) {
    this.model = model;
    this.endpoint = endpoint;
  }

  async embed(text: string): Promise<Float32Array> {
    const response = await fetch(`${this.endpoint}/api/embeddings`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ model: this.model, prompt: text }),
    });
    if (!response.ok) {
      throw new Error(`Embedding request failed: ${response.status} ${response.statusText}`);
    }
    const data = (await response.json()) as { embedding: number[] };
    return new Float32Array(data.embedding);
  }

  async isAvailable(): Promise<boolean> {
    // Return cached result if still valid
    if (this.availabilityCache !== null && Date.now() < this.availabilityCache[1]) {
      return this.availabilityCache[0];
    }

    try {
      const response = await fetch(this.endpoint, {
        signal: AbortSignal.timeout(EMBEDDING_AVAILABILITY_TIMEOUT_MS),
      });
      const available = response.ok;
      this.availabilityCache = [available, Date.now() + EMBEDDING_AVAILABILITY_CACHE_MS];
      return available;
    } catch {
      this.availabilityCache = [false, Date.now() + EMBEDDING_AVAILABILITY_CACHE_MS];
      return false;
    }
  }
}

export interface OpenAICompatibleEmbeddingConfig {
  baseUrl: string;
  model: string;
  apiKey?: string;
}

export class OpenAICompatibleEmbeddingService implements IEmbeddingService {
  private readonly baseUrl: string;
  private readonly model: string;
  private readonly apiKey?: string;
  private availabilityCache: [boolean, number] | null = null;

  constructor({ baseUrl, model, apiKey }: OpenAICompatibleEmbeddingConfig) {
    this.baseUrl = baseUrl.replace(/\/+$/, '');
    this.model = model;
    this.apiKey = apiKey;
  }

  async embed(text: string): Promise<Float32Array> {
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    if (this.apiKey) {
      headers['Authorization'] = `Bearer ${this.apiKey}`;
    }
    const response = await fetch(`${this.baseUrl}/embeddings`, {
      method: 'POST',
      headers,
      body: JSON.stringify({ input: text, model: this.model }),
    });
    if (!response.ok) {
      throw new Error(`Embedding request failed: ${response.status} ${response.statusText}`);
    }
    const data = (await response.json()) as {
      data: Array<{ embedding: number[]; index: number }>;
    };
    const first = data.data[0];
    if (!first) {
      throw new Error('Embedding response contained no data');
    }
    return new Float32Array(first.embedding);
  }

  async isAvailable(): Promise<boolean> {
    if (this.availabilityCache !== null && Date.now() < this.availabilityCache[1]) {
      return this.availabilityCache[0];
    }
    try {
      const headers: Record<string, string> = { 'Content-Type': 'application/json' };
      if (this.apiKey) {
        headers['Authorization'] = `Bearer ${this.apiKey}`;
      }
      const response = await fetch(`${this.baseUrl}/embeddings`, {
        method: 'POST',
        headers,
        body: JSON.stringify({ input: 'test', model: this.model }),
        signal: AbortSignal.timeout(EMBEDDING_AVAILABILITY_TIMEOUT_MS),
      });
      const available = response.ok;
      this.availabilityCache = [available, Date.now() + EMBEDDING_AVAILABILITY_CACHE_MS];
      return available;
    } catch {
      this.availabilityCache = [false, Date.now() + EMBEDDING_AVAILABILITY_CACHE_MS];
      return false;
    }
  }
}

export class NoopEmbeddingService implements IEmbeddingService {
  async isAvailable(): Promise<boolean> {
    return false;
  }

  async embed(_text: string): Promise<Float32Array> {
    throw new Error('No embedding provider configured');
  }
}
