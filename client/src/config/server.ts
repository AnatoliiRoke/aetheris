/**
 * Connection settings for the game server.
 *
 * The values come from Vite environment variables so that the same build can point at a
 * local server, a test server or a deployed instance.
 */
export const serverConfig = {
  /** Base HTTP address of the API. */
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080',

  /** SignalR hub address. */
  hubUrl: import.meta.env.VITE_HUB_URL ?? 'http://localhost:5080/hubs/game',
} as const;
