import React from 'react';
import { Text } from 'ink';
import { reindexEntries as reindexEntriesInCore } from 'ten-second-tom-core';
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
  const total = await services.storage.countEntries();

  if (total === 0) {
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
    content: <Text dimColor>Re-indexing {total} entries...</Text>,
  });

  const result = await reindexEntriesInCore(services);

  pushHistory({
    id: `reindex-done-${Date.now()}`,
    content: (
      <Text color="green">
        Re-indexed {result.total} entries ({result.updated} updated
        {result.failed > 0 ? `, ${result.failed} failed` : ''})
      </Text>
    ),
  });
}
