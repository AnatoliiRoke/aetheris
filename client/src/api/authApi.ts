import { ApiError, type HttpClient } from './http';

/**
 * The sign-in providers the server has enabled.
 */
export interface AuthProviders {
  google: boolean;
}

/**
 * The civilization of the player (task 00006): the race and the home planet.
 */
export interface Civilization {
  raceId: number;
  raceCode: string;
  planetId: number;
  planetName: string | null;
  systemId: number;
  sectorId: number;
}

/**
 * The signed-in account as the server describes it in `GET /api/v1/auth/me`.
 */
export interface Me {
  userId: string;
  nickname: string;
  email: string;
  emailConfirmed: boolean;
  nicknameChangedAt: string | null;
  civilization: Civilization | null;
}

/**
 * A new account (task 00006, decision 1).
 */
export interface Registration {
  email: string;
  nickname: string;
  password: string;
  passwordConfirmation: string;
}

/**
 * The account endpoints of the server (tasks 00002 and 00006, `/api/v1/auth` and
 * `/api/v1/account`).
 */
export interface AuthApi {
  register(registration: Registration): Promise<void>;
  resendConfirmation(email: string): Promise<void>;
  confirmEmail(userId: string, token: string): Promise<void>;
  /** Signs in with an e-mail address or a nickname; tells whether a pending deletion was cancelled. */
  login(login: string, password: string, rememberMe: boolean): Promise<{ deletionCancelled: boolean }>;
  logout(): Promise<void>;
  forgotPassword(email: string): Promise<void>;
  resetPassword(userId: string, token: string, newPassword: string): Promise<void>;
  /** The signed-in account, or `null` for a visitor. */
  me(): Promise<Me | null>;
  providers(): Promise<AuthProviders>;
  /** The address that starts the Google sign-in; the browser goes there. */
  googleSignInUrl(returnUrl: string): string;
  /** Sends the confirmation e-mail of the signed-in account again. */
  resendMyConfirmation(): Promise<void>;
  changePassword(currentPassword: string, newPassword: string, newPasswordConfirmation: string): Promise<void>;
  changeNickname(nickname: string): Promise<{ nickname: string; nextChangeAt: string }>;
  signOutEverywhere(): Promise<void>;
  /** Requests the deletion of the account; it happens after a delay unless the player signs in. */
  deleteAccount(password: string): Promise<{ deletionDueAt: string }>;
}

const prefix = '/api/v1/auth';
const accountPrefix = '/api/v1/account';

interface MeAnswer {
  user_id: string;
  nickname: string;
  email: string;
  email_confirmed: boolean;
  nickname_changed_at: string | null;
  civilization: CivilizationAnswer | null;
}

/**
 * The civilization in the snake_case of the server.
 */
export interface CivilizationAnswer {
  race_id: number;
  race_code: string;
  planet_id: number;
  planet_name: string | null;
  system_id: number;
  sector_id: number;
}

/**
 * Turns the answer of the server into the shape of the client.
 */
export function toCivilization(answer: CivilizationAnswer): Civilization {
  return {
    raceId: answer.race_id,
    raceCode: answer.race_code,
    planetId: answer.planet_id,
    planetName: answer.planet_name,
    systemId: answer.system_id,
    sectorId: answer.sector_id,
  };
}

/**
 * The account endpoints over HTTP.
 */
export function createAuthApi(http: HttpClient): AuthApi {
  return {
    async register({ email, nickname, password, passwordConfirmation }) {
      await http.post(`${prefix}/register`, { email, nickname, password, password_confirmation: passwordConfirmation });
    },
    async resendConfirmation(email) {
      await http.post(`${prefix}/resend-confirmation`, { email });
    },
    async confirmEmail(userId, token) {
      await http.post(`${prefix}/confirm-email`, { userId, token });
    },
    async login(login, password, rememberMe) {
      const answer = await http.post<{ deletion_cancelled?: boolean } | null>(`${prefix}/login`, {
        login,
        password,
        remember_me: rememberMe,
      });
      // The anti-forgery token belongs to the session, which has just changed.
      http.resetCsrf();
      return { deletionCancelled: answer?.deletion_cancelled === true };
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
        const answer = await http.get<MeAnswer>(`${prefix}/me`);
        return {
          userId: answer.user_id,
          nickname: answer.nickname,
          email: answer.email,
          emailConfirmed: answer.email_confirmed,
          nicknameChangedAt: answer.nickname_changed_at,
          civilization: answer.civilization ? toCivilization(answer.civilization) : null,
        };
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
    async resendMyConfirmation() {
      await http.command(`${accountPrefix}/resend-confirmation`, {});
    },
    async changePassword(currentPassword, newPassword, newPasswordConfirmation) {
      await http.command(`${accountPrefix}/change-password`, {
        current_password: currentPassword,
        new_password: newPassword,
        new_password_confirmation: newPasswordConfirmation,
      });
    },
    async changeNickname(nickname) {
      const answer = await http.command<{ nickname: string; next_change_at: string }>(`${accountPrefix}/change-nickname`, { nickname });
      return { nickname: answer.nickname, nextChangeAt: answer.next_change_at };
    },
    async signOutEverywhere() {
      try {
        await http.command(`${accountPrefix}/sign-out-everywhere`, {});
      } finally {
        http.resetCsrf();
      }
    },
    async deleteAccount(password) {
      try {
        const answer = await http.command<{ deletion_due_at: string }>(`${accountPrefix}/delete`, { password });
        return { deletionDueAt: answer.deletion_due_at };
      } finally {
        http.resetCsrf();
      }
    },
  };
}
