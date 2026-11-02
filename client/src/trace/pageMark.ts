/**
 * The mark of one transition to a page of the game: when it started and which requests belong to
 * it. Development only (task 00036): the production build never calls this module.
 */
interface PageMark {
  path: string;
  startedAt: number;
  requests: number;
  pending: number;
}

let navigationStartedAt: number | null = null;
let currentMark: PageMark | null = null;
let lastFinishedPath: string | null = null;

/**
 * Remembers the moment of a navigation, so the time of the page counts from the click and not
 * from the moment the page was drawn.
 */
export function markNavigationStart(): void {
  navigationStartedAt = performance.now();
}

/**
 * Starts the mark of a page. The path of a running or of a just finished mark is not started
 * again, so the repeated effects of the strict mode write the line once.
 */
export function beginPageTrace(path: string): void {
  if (currentMark?.path === path || lastFinishedPath === path) {
    return;
  }

  lastFinishedPath = null;

  // Without a navigation the page was opened by the browser: the time counts from its start.
  currentMark = { path, startedAt: navigationStartedAt ?? 0, requests: 0, pending: 0 };
  navigationStartedAt = null;
}

/**
 * Counts a request that starts while a page mark runs.
 *
 * @returns The mark the request belongs to, to give back to `pageRequestFinished`.
 */
export function pageRequestStarted(): PageMark | null {
  if (currentMark) {
    currentMark.requests += 1;
    currentMark.pending += 1;
  }

  return currentMark;
}

/**
 * Closes a request of a mark; the page is ready when no request of its mark is still running.
 */
export function pageRequestFinished(owner: PageMark | null): void {
  if (!owner) {
    return;
  }

  owner.pending -= 1;

  if (owner === currentMark) {
    finishIfIdle();
  }
}

/**
 * Called after the effects of the page have started their reads: a page that asked for nothing
 * (everything was read before) is ready at once.
 */
export function settlePageTrace(): void {
  finishIfIdle();
}

function finishIfIdle(): void {
  if (!currentMark || currentMark.pending > 0) {
    return;
  }

  const { path, startedAt, requests } = currentMark;
  currentMark = null;
  lastFinishedPath = path;
  console.debug(`[trace] page ${path} ready in ${Math.round(performance.now() - startedAt)} ms (${requests} requests)`);
}
