export const SENTIMENT_POSITIVE_THRESHOLD = 0.2;
export const SENTIMENT_NEGATIVE_THRESHOLD = -0.2;

/**
 * Format a sentiment score with sign prefix.
 * Shared between SentimentDisplay and SearchResults.
 */
export function formatScore(score: number): string {
  const sign = score >= 0 ? '+' : '';
  return `${sign}${score.toFixed(2)}`;
}

/**
 * Get the display color for a sentiment score.
 * Shared between SentimentDisplay and SearchResults components.
 */
export function getSentimentColor(score: number): string {
  if (score > SENTIMENT_POSITIVE_THRESHOLD) return 'green';
  if (score < SENTIMENT_NEGATIVE_THRESHOLD) return 'red';
  return 'yellow';
}

/**
 * Get a colored circle emoji for a sentiment score.
 * Used in SearchResults for compact inline display.
 */
export function getSentimentEmoji(score: number): string {
  if (score > SENTIMENT_POSITIVE_THRESHOLD) return '\u{1F7E2}'; // green circle
  if (score < SENTIMENT_NEGATIVE_THRESHOLD) return '\u{1F534}'; // red circle
  return '\u{1F7E1}'; // yellow circle
}
