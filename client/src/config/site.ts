/**
 * Settings of the public site that are not texts.
 *
 * The studio name and the support address are placeholders of the design
 * (documentation/design/aetheris-web 1/README.md, "TODO перед релізом"); they are replaced
 * before the release.
 */
export const siteConfig = {
  studioName: import.meta.env.VITE_STUDIO_NAME ?? '[STUDIO NAME]',
  supportEmail: import.meta.env.VITE_SUPPORT_EMAIL ?? 'support@example.com',
  version: __APP_VERSION__,

  /** The year of the copyright line, taken when the page loads. */
  copyrightYear: new Date().getFullYear(),

  /**
   * The password rules shown in the hints and checked before sending. They mirror the
   * starting values of `security.password.*` on the server; the server stays the authority
   * and answers with its own rule codes.
   */
  passwordMinLength: 8,

  /**
   * The nickname length shown in the hints and checked before sending; it mirrors
   * `security.account.nickname_min_length` and `nickname_max_length`.
   */
  nicknameMinLength: 3,
  nicknameMaxLength: 20,

  /**
   * The characters of a nickname; it mirrors `security.account.nickname_characters`
   * (listed as they are, `a-z` is a range, a dash at the end is the dash itself).
   */
  nicknameCharacters: 'A-Za-z\u0400-\u04FF0-9 _-',

  /** The length of the name of the home planet; it mirrors `world.civilization.home_name_*`. */
  planetNameMinLength: 3,
  planetNameMaxLength: 20,

  /**
   * The characters of the name of the home planet, with the plain and the typographic
   * apostrophe; it mirrors `world.civilization.home_name_characters`.
   */
  planetNameCharacters: "A-Za-z\u0400-\u04FF0-9 _'\u2019-",

  /** Seconds before the confirmation email can be requested again (`security.account.confirmation_resend_minutes`). */
  resendCooldownSeconds: 300,
} as const;
