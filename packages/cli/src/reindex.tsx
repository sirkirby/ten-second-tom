import React from 'react';
import { Text } from 'ink';
import type { ServiceContainer } from 'ten-second-tom-core';
import type { HistoryEntry } from './commands/registry.js';

/**
 * Re-embed all entries for semantic search.
 * Pushes progress messages via the pushHistory callback.
 */
export async function reindexEntries(
  services: ServiceContainer,
  pushHistory: (entry: HistoryEntry) => void,
): Promise<void> {
  const entries = await services.storage.listEntries({ limit: 100_000 });

  if (entries.length === 0) {
    pushHistory({
      id: `reindex-empty-${Date.now()}`,
      content: <Text dimColor>No entries to re-index.</Text>,
    });
    return;
  }

  const available = await services.embedding.isAvailable();
  if (!available) {
    pushHistory({
      id: `reindex-unavailable-${Date.now()}`,
      content: <Text color="yellow">Embedding service unavailable. Check Ollama is running.</Text>,
    });
    return;
  }

  pushHistory({
    id: `reindex-start-${Date.now()}`,
    content: <Text dimColor>Re-indexing {entries.length} entries...</Text>,
  });

  let updated = 0;
  let failed = 0;
  for (const entry of entries) {
    try {
      const embedding = await services.embedding.embed(entry.content);
      await services.storage.updateEntryEmbedding(entry.id, embedding);
      updated++;
    } catch {
      failed++;
    }
  }

  pushHistory({
    id: `reindex-done-${Date.now()}`,
    content: (
      <Text color="green">
        Re-indexed {entries.length} entries ({updated} updated
        {failed > 0 ? `, ${failed} failed` : ''})
      </Text>
    ),
  });
}
