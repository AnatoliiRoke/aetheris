import { serverConfig } from '../config/server';
import { pageRequestFinished, pageRequestStarted } from './pageMark';

/**
 * How a request ended, for the line in the console.
 */
export type RequestOutcome = 'ok' | 'error' | 'timeout' | 'network';

/**
 * The facts of one request. Only metadata: never a body, a header value or a cookie.
 */
export interface RequestRecord {
  method: string;
  path: string;
  requestId: string;
  status: number | null;
  outcome: RequestOutcome;
  clientMs: number;
  /** The value of the `Server-Timing` header of the answer, if there was one. */
  serverTiming: string | null;
}

/**
 * What the server said about its own time (`total;dur=N, db;dur=N;desc="K queries"`).
 */
export interface ServerTiming {
  totalMs: number | null;
  dbMs: number | null;
  queries: number | null;
}

/**
 * Reads the `Server-Timing` header of the server.
 *
 * @param header The value of the header.
 * @returns The times, or `null` when the answer had none.
 */
export function parseServerTiming(header: string | null): ServerTiming | null {
  if (!header) {
    return null;
  }

  const timing: ServerTiming = { totalMs: null, dbMs: null, queries: null };

  for (const metric of header.split(',')) {
    const name = metric.split(';')[0]?.trim();
    const duration = /dur=([\d.]+)/.exec(metric);
    const description = /desc="?(\d+)/.exec(metric);

    if (name === 'total' && duration) {
      timing.totalMs = Number(duration[1]);
    } else if (name === 'db') {
      timing.dbMs = duration ? Number(duration[1]) : null;
      timing.queries = description ? Number(description[1]) : null;
    }
  }

  return timing.totalMs === null && timing.dbMs === null ? null : timing;
}

/**
 * Tells the trace that a request starts.
 *
 * @returns What to give back to `traceRequestFinished`.
 */
export function traceRequestStarted(): ReturnType<typeof pageRequestStarted> {
  return pageRequestStarted();
}

/**
 * Writes the one line of a request into the console of the browser: `console.debug` for an
 * ordinary request, `console.warn` for a slow one, an error, a timeout or a lost connection.
 * Called in development only.
 */
export function traceRequestFinished(record: RequestRecord, owner: ReturnType<typeof pageRequestStarted>): void {
  const timing = parseServerTiming(record.serverTiming);
  const slow = record.clientMs >= serverConfig.slowRequestMs;
  const parts = [`${record.method} ${withoutQuery(record.path)}`, record.status === null ? record.outcome : String(record.status), `${Math.round(record.clientMs)} ms`];

  if (timing) {
    parts.push(`server ${format(timing.totalMs)} ms, db ${format(timing.dbMs)} ms in ${timing.queries ?? '?'} queries`);
  }

  parts.push(`id ${record.requestId}`);

  const flags = [slow ? 'SLOW' : null, record.outcome === 'ok' ? null : record.outcome.toUpperCase()].filter(Boolean);
  const line = `[api] ${flags.map((flag) => `${flag} `).join('')}${parts.join(' | ')}`;

  if (flags.length > 0) {
    console.warn(line);
  } else {
    console.debug(line);
  }

  pageRequestFinished(owner);
}

/**
 * The query string can carry a one-time token (a confirmation link), so it is never written.
 */
function withoutQuery(path: string): string {
  const queryStart = path.indexOf('?');
  return queryStart < 0 ? path : path.slice(0, queryStart);
}

function format(milliseconds: number | null): string {
  return milliseconds === null ? '?' : String(Math.round(milliseconds));
}
