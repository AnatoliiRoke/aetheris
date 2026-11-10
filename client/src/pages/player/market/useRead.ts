import { useCallback, useEffect, useRef, useState } from 'react';
import { useReloadOnVisible } from '../tabVisibility';

/**
 * The state of a read of the server.
 */
export interface ReadState<T> {
  data: T | null;
  /** The real time the last answer came, in milliseconds, or `null` before the first answer. */
  receivedAt: number | null;
  loading: boolean;
  failed: boolean;
  /** Reads again; the data of the last answer stays on the screen while the new one comes. */
  reload: () => Promise<void>;
}

/**
 * Reads a thing from the server when the page opens or when the read changes, and reads it again
 * when the tab becomes visible. A newer read wins: an old answer that comes late is dropped.
 *
 * @param load The read; it must be stable, so it changes only when what is read changes.
 * @param enabled Whether to read at all; a page that waits for a choice passes `false`.
 * @param reloadOnVisible Whether to read again when the tab of the browser becomes visible. A read
 * of the data that never changes passes `false`, so the server is not asked for it again and again.
 */
export function useRead<T>(load: () => Promise<T>, enabled = true, reloadOnVisible = true): ReadState<T> {
  const [state, setState] = useState<{ data: T | null; receivedAt: number | null; failed: boolean }>({ data: null, receivedAt: null, failed: false });
  const sequence = useRef(0);

  const apply = useCallback((answer: Promise<T>, mine: number) =>
    answer
      .then((data) => {
        if (mine === sequence.current) {
          setState({ data, receivedAt: Date.now(), failed: false });
        }
      })
      .catch(() => {
        if (mine === sequence.current) {
          setState((previous) => ({ ...previous, failed: true }));
        }
      }), []);

  const run = useCallback(() => apply(load(), ++sequence.current), [apply, load]);

  useEffect(() => {
    if (!enabled) {
      return undefined;
    }

    const mine = ++sequence.current;

    void apply(load(), mine);

    return () => {
      sequence.current += 1;
    };
  }, [apply, load, enabled]);

  useReloadOnVisible(enabled && reloadOnVisible ? run : noop);

  return { ...state, loading: enabled && state.data === null && !state.failed, reload: run };
}

function noop(): void {}
