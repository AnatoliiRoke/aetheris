import type { ReactNode } from 'react';
import { artSize, images } from '../../assets/images';
import { ArrowLeftIcon } from '../../components/Icons';
import { LangSwitch } from '../../components/LangSwitch';
import { LogoContent } from '../../components/Logo';
import { siteConfig } from '../../config/site';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';

/**
 * The frame of the account screens: the art on the left on a wide screen, a banner on a
 * phone, the form on the right.
 */
export function AuthLayout({ children }: { children: ReactNode }) {
  const { t } = useI18n();

  return (
    <div className="auth">
      <aside className="auth__side" aria-hidden="true">
        <img src={images.authSide} alt="" {...artSize} />
        <div className="auth__side-logo logo">
          <LogoContent />
        </div>
        <div className="auth__side-copy">
          <p className="auth__side-title">{t('auth.side.title')}</p>
          <p className="lead">{t('auth.side.lead')}</p>
        </div>
      </aside>

      <main className="auth__main">
        <div className="auth__top">
          <Link className="back-link" to={routes.home}>
            <ArrowLeftIcon />
            <span>{t('common.home')}</span>
          </Link>
          <LangSwitch />
        </div>

        <div className="auth__banner" aria-hidden="true">
          <img src={images.authSide} alt="" {...artSize} />
          <span className="logo__text">AETHERIS</span>
        </div>

        <div className="auth__content">{children}</div>

        <p className="auth__legal">
          {t('footer.copyright', { year: siteConfig.copyrightYear, studio: siteConfig.studioName })} ·{' '}
          <a href="/#terms">{t('auth.legalTerms')}</a> · <a href="/#privacy">{t('auth.legalPrivacy')}</a>
        </p>
      </main>
    </div>
  );
}
