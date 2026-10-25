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
} as const;
