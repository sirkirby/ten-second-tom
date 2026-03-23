import { useEffect } from 'react';
import { useApp } from 'ink';

/**
 * Auto-exit the Ink app after a delay when the given condition is true.
 * Replaces the duplicated 5-second auto-exit useEffect found in
 * record, note, analyze, and setup commands.
 *
 * @param shouldExit  - When true, the exit timer begins.
 * @param delayMs     - Milliseconds to wait before calling exit().
 * @param enabled     - Master switch. When false the hook is inert (useful in
 *                      REPL mode where the app should not auto-close).
 */
export function useAutoExit(shouldExit: boolean, delayMs: number = 5000, enabled: boolean = true) {
  const { exit } = useApp();
  useEffect(() => {
    if (!enabled || !shouldExit) return undefined;
    const timer = setTimeout(() => exit(), delayMs);
    return () => clearTimeout(timer);
  }, [shouldExit, delayMs, exit, enabled]);
}
