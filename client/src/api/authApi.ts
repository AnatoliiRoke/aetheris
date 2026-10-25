import { ApiError, type HttpClient } from './http';

/**
 * The sign-in providers the server has enabled.
 */
export interface AuthProviders {
  google: boolean;
}

/**
 * The account endpoints of the server (task 00002, `/api/v1/auth`).
 */
export interface AuthApi {
  register(email: string, password: string): Promise<void>;
  resendConfirmation(email: string): Promise<void>;
  confirmEmail(userId: string, token: string): Promise<void>;
  login(email: string, password: string): Promise<void>;
  logout(): Promise<void>;
  forgotPassword(email: string): Promise<void>;
  resetPassword(userId: string, token: string, newPassword: string): Promise<void>;
  /** The signed-in account, or `null` for a visitor. */
  me(): Promise<{ userId: string } | null>;
  providers(): Promise<AuthProviders>;
  /** The address that starts the Google sign-in; the browser goes there. */
  googleSignInUrl(returnUrl: string): string;
}

const prefix = '/api/v1/auth';

/**
 * The account endpoints over HTTP.
 */
export function createAuthApi(http: HttpClient): AuthApi {
  return {
    async register(email, password) {
      await http.post(`${prefix}/register`, { email, password });
    },
    async resendConfirmation(email) {
      await http.post(`${prefix}/resend-confirmation`, { email });
    },
    async confirmEmail(userId, token) {
      await http.post(`${prefix}/confirm-email`, { userId, token });
    },
    async login(email, password) {
      await http.post(`${prefix}/login`, { email, password });
      // The anti-forgery token belongs to the session, which has just changed.
      http.resetCsrf();
    },
    async logout() {
      try {
        await http.post(`${prefix}/logout`);
      } finally {
        http.resetCsrf();
      }
    },
    async forgotPassword(email) {
      await http.post(`${prefix}/forgot-password`, { email });
    },
    async resetPassword(userId, token, newPassword) {
      await http.post(`${prefix}/reset-password`, { userId, token, newPassword });
    },
    async me() {
      try {
        return await http.get<{ userId: string }>(`${prefix}/me`);
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) {
          return null;
        }

        throw error;
      }
    },
    providers() {
      return http.get<AuthProviders>(`${prefix}/providers`);
    },
    googleSignInUrl(returnUrl) {
      return http.url(`${prefix}/google?returnUrl=${encodeURIComponent(returnUrl)}`);
    },
  };
}
