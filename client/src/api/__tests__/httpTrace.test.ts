import { afterEach, beforeEach, describe, expect, it, vi, type MockInstance } from 'vitest';
import { serverConfig } from '../../config/server';
import { parseServerTiming } from '../../trace/requestTrace';
import { HttpClient, requestIdHeader, serverTimingHeader } from '../http';

// The rule of the server for a client identifier (task 00036): letters, digits and `-_.:`, up to
// `observability.http_trace.request_id_max_length`.
const serverRequestIdPattern = /^[A-Za-z0-9\-_.:]{1,64}$/;

function answer(status: number, body: unknown, headers: Record<string, string> = {}): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json', ...headers } });
}

function headersOf(fetchMock: ReturnType<typeof vi.fn>, call: number): Record<string, string> {
  return (fetchMock.mock.calls[call][1] as RequestInit).headers as Record<string, string>;
}

describe('request identifier', () => {
  it('names every request, the token and the renewal included, with a new valid identifier', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(answer(200, { token: 'csrf-1' }))
      .mockResolvedValueOnce(answer(401, { error: 'not_authenticated' }))
      .mockResolvedValueOnce(answer(200, {}))
      .mockResolvedValueOnce(answer(200, { token: 'csrf-2' }))
      .mockResolvedValueOnce(answer(200, { ok: true }));
    const http = new HttpClient('', fetchMock);

    await http.post('/api/v1/planets/1/queue', { a: 1 });

    const identifiers = fetchMock.mock.calls.map((_, call) => headersOf(fetchMock, call)[requestIdHeader]);
    expect(identifiers).toHaveLength(5);
    expect(new Set(identifiers).size).toBe(5);

    for (const identifier of identifiers) {
      expect(identifier).toMatch(serverRequestIdPattern);
    }

    expect(fetchMock.mock.calls.map((call) => call[0])).toEqual(['/api/v1/auth/csrf', '/api/v1/planets/1/queue', '/api/v1/auth/refresh', '/api/v1/auth/csrf', '/api/v1/planets/1/queue']);
  });
});

describe('trace line of a request in development', () => {
  let debug: MockInstance<typeof console.debug>;
  let warn: MockInstance<typeof console.warn>;

  beforeEach(() => {
    debug = vi.spyOn(console, 'debug').mockImplementation(() => undefined);
    warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined);
  });

  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it('writes one debug line with the method, the path, the status, the times and the identifier', async () => {
    const fetchMock = vi.fn().mockResolvedValue(answer(200, {}, { [serverTimingHeader]: 'total;dur=31.4, db;dur=12.2;desc="5 queries"' }));
    const http = new HttpClient('', fetchMock);

    await http.get('/api/v1/planets/1');

    expect(debug).toHaveBeenCalledTimes(1);
    expect(warn).not.toHaveBeenCalled();

    const line = String(debug.mock.calls[0][0]);
    const identifier = headersOf(fetchMock, 0)[requestIdHeader];
    expect(line).toContain('GET /api/v1/planets/1');
    expect(line).toContain('200');
    expect(line).toMatch(/\d+ ms/);
    expect(line).toContain('server 31 ms, db 12 ms in 5 queries');
    expect(line).toContain(identifier);
  });

  it('writes a line for the token request too', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(answer(200, { token: 'csrf-1' }))
      .mockResolvedValueOnce(answer(200, {}));
    const http = new HttpClient('', fetchMock);

    await http.post('/api/v1/planets/1/queue', {});

    expect(debug).toHaveBeenCalledTimes(2);
    expect(String(debug.mock.calls[0][0])).toContain('GET /api/v1/auth/csrf');
    expect(String(debug.mock.calls[1][0])).toContain('POST /api/v1/planets/1/queue');
  });

  it('warns about a slow request', async () => {
    const times = [0, serverConfig.slowRequestMs + 5];
    vi.spyOn(performance, 'now').mockImplementation(() => times.shift() ?? 0);
    const http = new HttpClient('', vi.fn().mockResolvedValue(answer(200, {})));

    await http.get('/api/v1/planets/1');

    expect(debug).not.toHaveBeenCalled();
    expect(warn).toHaveBeenCalledTimes(1);
    expect(String(warn.mock.calls[0][0])).toContain('SLOW');
  });

  it('warns about an error answer, a timeout and a lost connection, each marked by its own word', async () => {
    const timeout = new DOMException('The operation timed out.', 'TimeoutError');
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(answer(500, { error: 'internal_error' }))
      .mockRejectedValueOnce(timeout)
      .mockRejectedValueOnce(new TypeError('Failed to fetch'));
    const http = new HttpClient('', fetchMock);

    await expect(http.get('/api/v1/a')).rejects.toMatchObject({ status: 500 });
    await expect(http.get('/api/v1/b')).rejects.toMatchObject({ code: 'network_error' });
    await expect(http.get('/api/v1/c')).rejects.toMatchObject({ code: 'network_error' });

    expect(debug).not.toHaveBeenCalled();
    const lines = warn.mock.calls.map((call) => String(call[0]));
    expect(lines).toHaveLength(3);
    expect(lines[0]).toContain('[api] ERROR GET /api/v1/a | 500');
    expect(lines[1]).toContain('[api] TIMEOUT GET /api/v1/b | timeout');
    expect(lines[2]).toContain('[api] NETWORK GET /api/v1/c | network');
  });

  it('never writes a body, a password, a cookie or the query string', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(answer(200, { token: 'csrf-secret-token' }))
      .mockResolvedValueOnce(answer(200, { refresh: 'refresh-secret' }, { 'Set-Cookie': 'session=cookie-secret', [serverTimingHeader]: 'total;dur=3' }));
    const http = new HttpClient('', fetchMock);

    await http.post('/api/v1/auth/verify?token=query-secret', { password: 'body-secret', email: 'player@example.test' });

    const output = JSON.stringify([...debug.mock.calls, ...warn.mock.calls]);

    for (const secret of ['csrf-secret-token', 'body-secret', 'player@example.test', 'refresh-secret', 'cookie-secret', 'query-secret', 'Set-Cookie', 'Content-Type']) {
      expect(output).not.toContain(secret);
    }
  });

  it('writes nothing in the production build', async () => {
    vi.stubEnv('DEV', false);
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(answer(200, { token: 'csrf-1' }))
      .mockResolvedValueOnce(answer(500, { error: 'internal_error' }))
      .mockRejectedValueOnce(new TypeError('Failed to fetch'));
    const http = new HttpClient('', fetchMock);

    await expect(http.post('/api/v1/a', {})).rejects.toMatchObject({ status: 500 });
    await expect(http.post('/api/v1/b', {})).rejects.toMatchObject({ code: 'network_error' });

    expect(debug).not.toHaveBeenCalled();
    expect(warn).not.toHaveBeenCalled();
    // The identifier does not depend on the mode.
    expect(headersOf(fetchMock, 1)[requestIdHeader]).toMatch(serverRequestIdPattern);
  });
});

describe('Server-Timing header', () => {
  it('is read with the total, the database time and the number of commands', () => {
    expect(parseServerTiming('total;dur=31.4, db;dur=12.2;desc="5 queries"')).toEqual({ totalMs: 31.4, dbMs: 12.2, queries: 5 });
    expect(parseServerTiming('total;dur=3')).toEqual({ totalMs: 3, dbMs: null, queries: null });
  });

  it('gives nothing for a missing or foreign header', () => {
    expect(parseServerTiming(null)).toBeNull();
    expect(parseServerTiming('cache;desc="hit"')).toBeNull();
  });
});
