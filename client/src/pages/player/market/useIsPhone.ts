import { useSyncExternalStore } from 'react';

/**
 * The widest screen that is a phone: the mock-up swaps the table for cards below it.
 */
const phoneQuery = '(max-width: 767px)';

function subscribe(callback: () => void): () => void {
  const list = window.matchMedia?.(phoneQuery);

  list?.addEventListener?.('change', callback);

  return () => list?.removeEventListener?.('change', callback);
}

function snapshot(): boolean {
  return window.matchMedia?.(phoneQuery).matches ?? false;
}

/**
 * Tells whether the screen is a phone, so the market draws cards where the desktop draws a table.
 * One layout is drawn at a time, so the page never carries both.
 */
export function useIsPhone(): boolean {
  return useSyncExternalStore(subscribe, snapshot, () => false);
}
