import { serverConfig } from '../config/server';

/**
 * The error codes the client adds to the server's own codes (documentation/tech/04, section 8).
 */
export const clientErrorCodes = {
  network: 'network_error',
  unknown: 'internal_error',
} as const;

/**
 * An error answer of the server: `{ "error": "code", "details": {...} }`.
 */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly details: Record<string, unknown>;

  constructor(status: number, code: string, details: Record<string, unknown> = {}) {
    super(code);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.details = details;
  }
}

/**
 * The anti-forgery header and the request identifier header of the server (task 00002).
 */
export const csrfHeader = 'X-XSRF-TOKEN';
export const idempotencyHeader = 'Idempotency-Key';

const csrfPath = '/api/v1/auth/csrf';
const csrfErrorCode = 'csrf_token_invalid';
const refreshPath = '/api/v1/auth/refresh';
const authPrefix = '/api/v1/auth/';
const mePath = '/api/v1/auth/me';
const adminPrefix = '/api/admin/';

interface RequestOptions {
  body?: unknown;
  file?: Blob;
  idempotencyKey?: string;
}

type FetchFunction = typeof fetch;

/**
 * Talks to the server the way the security layer of task 00002 expects:
 * - the session lives in `HttpOnly` cookies, so every request is sent with credentials;
 * - every state-changing request carries the anti-forgery token from `GET /api/v1/auth/csrf`;
 *   the token belongs to the current session, so it is fetched again after a sign-in or a
 *   sign-out, and once more when the server refuses it;
 * - a command carries a fresh `Idempotency-Key`, so a repeated click does not repeat it;
 * - the session cookie lives a few minutes; when the server answers `401`, the client renews
 *   the session once with the refresh cookie (`POST /api/v1/auth/refresh`) and repeats the
 *   request. With "remember me" this also brings the session back after the browser restarts.
 */
export class HttpClient {
  private readonly baseUrl: string;
  private readonly fetchFunction: FetchFunction;
  private csrfToken: Promise<string> | null = null;
  private refreshing: Promise<boolean> | null = null;

  constructor(baseUrl: string = serverConfig.apiBaseUrl, fetchFunction: FetchFunction = (...args) => fetch(...args)) {
    this.baseUrl = baseUrl;
    this.fetchFunction = fetchFunction;
  }

  /**
   * Builds the absolute address of a server route, for links and redirects.
   */
  url(path: string): string {
    return `${this.baseUrl}${path}`;
  }

  /**
   * Forgets the anti-forgery token, after the session changed.
   */
  resetCsrf(): void {
    this.csrfToken = null;
  }

  get<T>(path: string): Promise<T> {
    return this.send<T>('GET', path, {});
  }

  post<T>(path: string, body?: unknown): Promise<T> {
    return this.send<T>('POST', path, { body });
  }

  put<T>(path: string, body?: unknown): Promise<T> {
    return this.send<T>('PUT', path, { body });
  }

  /**
   * Sends a file as the body of the request, for the pictures of the admin panel (task 00023).
   */
  upload<T>(path: string, file: Blob): Promise<T> {
    return this.send<T>('POST', path, { file });
  }

  /**
   * Sends a command with a new request identifier.
   */
  command<T>(path: string, body: unknown): Promise<T> {
    return this.send<T>('POST', path, { body, idempotencyKey: crypto.randomUUID() });
  }

  private async send<T>(method: string, path: string, options: RequestOptions, isRetry = false): Promise<T> {
    const headers: Record<string, string> = { Accept: 'application/json' };

    if (options.file !== undefined) {
      headers['Content-Type'] = 'application/octet-stream';
    } else if (options.body !== undefined) {
      headers['Content-Type'] = 'application/json';
    }

    if (method !== 'GET') {
      headers[csrfHeader] = await this.getCsrfToken();
    }

    if (options.idempotencyKey) {
      headers[idempotencyHeader] = options.idempotencyKey;
    }

    let response: Response;

    try {
      response = await this.fetchFunction(this.url(path), {
        method,
        headers,
        credentials: 'include',
        body: options.file ?? (options.body === undefined ? undefined : JSON.stringify(options.body)),
      });
    } catch {
      throw new ApiError(0, clientErrorCodes.network);
    }

    if (response.ok) {
      return (await readJson(response)) as T;
    }

    const error = await toApiError(response);

    // The token was issued for another session, for example one that expired meanwhile.
    if (error.code === csrfErrorCode && !isRetry) {
      this.resetCsrf();
      return this.send<T>(method, path, options, true);
    }

    // The session cookie has expired; the refresh cookie may start a new one.
    if (response.status === 401 && !isRetry && canRefresh(path) && (await this.refreshSession())) {
      return this.send<T>(method, path, options, true);
    }

    throw error;
  }

  /**
   * Renews the session with the refresh cookie. Parallel callers share one attempt.
   */
  private refreshSession(): Promise<boolean> {
    if (!this.refreshing) {
      this.refreshing = this.send<unknown>('POST', refreshPath, {}, true)
        .then(
          () => true,
          () => false,
        )
        .finally(() => {
          // The anti-forgery token belongs to the session, which has just changed.
          this.resetCsrf();
          this.refreshing = null;
        });
    }

    return this.refreshing;
  }

  private getCsrfToken(): Promise<string> {
    if (!this.csrfToken) {
      this.csrfToken = this.fetchCsrfToken().catch((error: unknown) => {
        this.csrfToken = null;
        throw error;
      });
    }

    return this.csrfToken;
  }

  private async fetchCsrfToken(): Promise<string> {
    let response: Response;

    try {
      response = await this.fetchFunction(this.url(csrfPath), { credentials: 'include', headers: { Accept: 'application/json' } });
    } catch {
      throw new ApiError(0, clientErrorCodes.network);
    }

    if (!response.ok) {
      throw await toApiError(response);
    }

    const body = (await readJson(response)) as { token?: string } | null;
    return body?.token ?? '';
  }
}

/**
 * The account routes answer `401` for a reason of their own (a wrong password, an expired
 * link), so only the game routes and `/me` try to renew the session. The admin panel has its
 * own session without a refresh token (task 00007): its `401` means a new sign-in.
 */
function canRefresh(path: string): boolean {
  if (path.startsWith(adminPrefix)) {
    return false;
  }

  return path === mePath || !path.startsWith(authPrefix);
}

async function readJson(response: Response): Promise<unknown> {
  const text = await response.text();
  return text.length > 0 ? JSON.parse(text) : null;
}

async function toApiError(response: Response): Promise<ApiError> {
  try {
    const body = (await readJson(response)) as { error?: string; details?: Record<string, unknown> } | null;
    return new ApiError(response.status, body?.error ?? clientErrorCodes.unknown, body?.details ?? {});
  } catch {
    return new ApiError(response.status, clientErrorCodes.unknown);
  }
}
