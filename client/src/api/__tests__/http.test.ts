import { describe, expect, it, vi } from 'vitest';
import { ApiError, HttpClient, csrfHeader, idempotencyHeader } from '../http';

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

describe('HttpClient', () => {
  it('sends the anti-forgery token and the cookies with a state-changing request', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse(200, { token: 'csrf-1' }))
      .mockResolvedValueOnce(jsonResponse(200, { ok: true }));
    const http = new HttpClient('', fetchMock);

    await http.post('/api/v1/auth/login', { email: 'a@b.c' });

    expect(fetchMock.mock.calls[0][0]).toBe('/api/v1/auth/csrf');
    const [url, init] = fetchMock.mock.calls[1] as [string, RequestInit];
    expect(url).toBe('/api/v1/auth/login');
    expect(init.credentials).toBe('include');
    expect((init.headers as Record<string, string>)[csrfHeader]).toBe('csrf-1');
  });

  it('adds a request identifier to a command', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse(200, { token: 'csrf-1' }))
      .mockResolvedValueOnce(jsonResponse(200, {}));
    const http = new HttpClient('', fetchMock);

    await http.command('/api/v1/test/echo', { message: 'hi' });

    const headers = (fetchMock.mock.calls[1][1] as RequestInit).headers as Record<string, string>;
    expect(headers[idempotencyHeader]).toMatch(/^[0-9a-f-]{36}$/);
  });

  it('fetches a new token once when the server refuses the old one', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse(200, { token: 'old' }))
      .mockResolvedValueOnce(jsonResponse(403, { error: 'csrf_token_invalid' }))
      .mockResolvedValueOnce(jsonResponse(200, { token: 'new' }))
      .mockResolvedValueOnce(jsonResponse(200, {}));
    const http = new HttpClient('', fetchMock);

    await http.post('/api/v1/auth/logout');

    const headers = (fetchMock.mock.calls[3][1] as RequestInit).headers as Record<string, string>;
    expect(headers[csrfHeader]).toBe('new');
  });

  it('turns an error answer into an ApiError with the server code', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(jsonResponse(401, { error: 'not_authenticated' }));
    const http = new HttpClient('', fetchMock);

    await expect(http.get('/api/v1/auth/me')).rejects.toMatchObject({ status: 401, code: 'not_authenticated' });
  });

  it('reports a network failure', async () => {
    const http = new HttpClient('', vi.fn().mockRejectedValue(new TypeError('offline')));

    const error = await http.get('/api/v1/auth/me').catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ code: 'network_error' });
  });

  it('renews an expired session once with the refresh cookie and repeats the request', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse(401, { error: 'not_authenticated' }))
      .mockResolvedValueOnce(jsonResponse(200, { token: 'csrf-1' }))
      .mockResolvedValueOnce(jsonResponse(200, { user_id: 'user-1' }))
      .mockResolvedValueOnce(jsonResponse(200, [{ id: 1 }]));
    const http = new HttpClient('', fetchMock);

    await expect(http.get('/api/v1/world/races')).resolves.toEqual([{ id: 1 }]);

    expect(fetchMock.mock.calls.map((call) => call[0])).toEqual([
      '/api/v1/world/races',
      '/api/v1/auth/csrf',
      '/api/v1/auth/refresh',
      '/api/v1/world/races',
    ]);
  });

  it('does not renew the session for an account route such as the sign-in', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce(jsonResponse(200, { token: 'csrf-1' }))
      .mockResolvedValueOnce(jsonResponse(401, { error: 'invalid_credentials' }));
    const http = new HttpClient('', fetchMock);

    await expect(http.post('/api/v1/auth/login', {})).rejects.toMatchObject({ code: 'invalid_credentials' });
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });
});
