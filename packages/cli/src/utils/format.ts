/**
 * Shared formatting utilities used across CLI screens and components.
 */

/**
 * Format an ISO date string as "Mon DD" (e.g. "Mar 22").
 */
export function formatShortDate(isoDate: string): string {
  return new Date(isoDate).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
  });
}

/**
 * Format an ISO date string as "Mon DD, YYYY" (e.g. "Mar 22, 2026").
 */
export function formatFullDate(isoDate: string): string {
  return new Date(isoDate).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
}

/**
 * Return the first N characters of text (single line), with "..." suffix if truncated.
 */
export function getExcerpt(text: string, maxLength = 60): string {
  const oneLine = text.replace(/\n/g, ' ');
  if (oneLine.length <= maxLength) return oneLine;
  return oneLine.slice(0, maxLength) + '...';
}

/**
 * Format a confidence value (0-1) as a percentage string (e.g. "92%").
 */
export function formatConfidence(confidence: number): string {
  return `${Math.round(confidence * 100)}%`;
}

/**
 * Format a duration in seconds as "M:SS" (e.g. "1:05").
 */
export function formatDuration(seconds: number): string {
  const minutes = Math.floor(seconds / 60);
  const secs = seconds % 60;
  return `${minutes}:${String(secs).padStart(2, '0')}`;
}

/**
 * Extract a human-readable error message from an unknown thrown value.
 */
export function toErrorMessage(err: unknown): string {
  return err instanceof Error ? err.message : String(err);
}
