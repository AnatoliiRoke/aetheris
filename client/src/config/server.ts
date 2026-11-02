/**
 * Connection settings for the game server.
 *
 * The values come from Vite environment variables so that the same build can point at a
 * local server, a test server or a deployed instance. By default the API is called on the
 * same origin: in development the Vite server forwards `/api` and `/hubs` to the game
 * server, so the session cookies stay first-party.
 */
export const serverConfig = {
  /** Base HTTP address of the API; empty means the same origin as the page. */
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? '',

  /** SignalR hub address. */
  hubUrl: import.meta.env.VITE_HUB_URL ?? '/hubs/game',

  /**
   * How long a request may take before the client gives it up. Without it a request that never
   * gets an answer (the server is starting, the proxy has no answer) hangs until the browser ends
   * it after minutes, and every call that waits for it (the renewal of the session, the
   * anti-forgery token) hangs with it.
   */
  requestTimeoutMs: 20_000,

  /**
   * The pauses before the repeated reads of what failed to load, one pause per attempt; after the
   * last attempt the read waits for an event of the page or for the button "retry".
   */
  retryDelaysMs: [2_000, 5_000, 10_000, 20_000],
} as const;
