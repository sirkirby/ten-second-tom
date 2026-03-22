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
    const data = (await response.json()) as { embedding: number[] };
    return new Float32Array(data.embedding);
  }

  async isAvailable(): Promise<boolean> {
    try {
      const response = await fetch(this.endpoint);
      return response.ok;
    } catch {
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
