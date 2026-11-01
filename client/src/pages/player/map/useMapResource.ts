import { useCallback, useEffect, useRef, useState } from 'react';

/**
 * One read of the map: the answer, whether the read failed, and the way to read again. A change of
 * the key starts a new read; the answer the screen already had for a key is shown at once while
 * the new one comes, so going back up a level does not blink. There is no timer: a read happens on
 * the start, on a change of the key and on `reload`.
 *
 * @param key What is read (the level and the identifier), or `null` when nothing is to be read.
 * @param load The read; it may change between the draws, the last one is used.
 */
export function useMapResource<T>(key: string | null, load: () => Promise<T>) {
  const [state, setState] = useState<{ key: string | null; data: T | null; failed: boolean }>({ key: null, data: null, failed: false });
  const [cache] = useState(() => new Map<string, T>());
  const loadRef = useRef(load);

  useEffect(() => {
    loadRef.current = load;
  });

  const reload = useCallback(async () => {
    if (key === null) {
      return;
    }

    try {
      const data = await loadRef.current();

      cache.set(key, data);
      setState({ key, data, failed: false });
    } catch {
      setState((previous) => ({ key, data: previous.key === key ? previous.data : null, failed: true }));
    }
  }, [key, cache]);

  useEffect(() => {
    void reload();
  }, [reload]);

  const data = key === null ? null : state.key === key ? state.data : cache.get(key) ?? null;
  const failed = state.key === key && state.failed && data === null;

  return { data, failed, loading: key !== null && data === null && !failed, reload };
}
