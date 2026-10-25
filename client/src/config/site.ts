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
   * The smallest password length shown in the hints and checked before sending. It mirrors
   * the starting value of `security.password.min_length` on the server; the server stays the
   * authority and answers with its own rule codes.
   */
  passwordMinLength: 12,

  /** Seconds before the confirmation email can be requested again. */
  resendCooldownSeconds: 60,
} as const;
