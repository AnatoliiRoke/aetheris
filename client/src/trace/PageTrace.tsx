import { useEffect, useLayoutEffect } from 'react';
import { beginPageTrace, settlePageTrace } from './pageMark';

/**
 * Measures the time from the transition to a page to the moment its data came, in the console of
 * the browser (task 00036). It draws nothing and holds no state, so it never redraws the page; it
 * is rendered in development only. Put it after the page: the passive effects run in the order of
 * the tree, so its settling comes after the reads that the page started.
 */
export function PageTrace({ path }: { path: string }) {
  // The layout effect runs before the passive effects of the page start their reads.
  useLayoutEffect(() => beginPageTrace(path), [path]);
  useEffect(() => settlePageTrace(), [path]);

  return null;
}
