import { useEffect, useState } from 'react';

/**
 * A value that follows another one a moment after it stops changing, so a field that the player
 * types in does not send a read for every letter.
 *
 * @param value The value that changes.
 * @param delayMs How long it must stay the same, in milliseconds.
 */
export function useDebounced<T>(value: T, delayMs: number): T {
  const [settled, setSettled] = useState(value);

  useEffect(() => {
    const timer = window.setTimeout(() => setSettled(value), delayMs);

    return () => window.clearTimeout(timer);
  }, [value, delayMs]);

  return settled;
}
