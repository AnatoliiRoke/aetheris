import { useEffect } from 'react';

/**
 * Calls a function every time the tab of the browser becomes visible again, so a page that was
 * hidden reads the server once, never on a timer.
 *
 * @param callback What to do when the tab becomes visible.
 * @returns The function that stops the watch.
 */
export function onTabVisible(callback: () => void): () => void {
  const listener = () => {
    if (document.visibilityState === 'visible') {
      callback();
    }
  };

  document.addEventListener('visibilitychange', listener);

  return () => document.removeEventListener('visibilitychange', listener);
}

/**
 * Reads the data of a page again when the tab of the browser becomes visible. The one shared
 * helper of every hook that reads the server (task 00020, section 7.4).
 *
 * @param reload The function that reads the data.
 */
export function useReloadOnVisible(reload: () => unknown): void {
  useEffect(() => onTabVisible(() => void reload()), [reload]);
}
