import { siteConfig } from '../config/site';
import { useI18n } from '../i18n/useI18n';
import { ChatIcon, FeedIcon, VideoIcon } from './Icons';
import { LangSwitch } from './LangSwitch';
import { LogoContent } from './Logo';

/**
 * The site footer: language, documents, contacts, communities and the version.
 *
 * The documents and the community links are placeholders of the design until the pages
 * and the channels exist.
 */
export function SiteFooter({ onLanding }: { onLanding: boolean }) {
  const { t } = useI18n();
  const year = siteConfig.copyrightYear;

  return (
    <footer className="site-footer">
      <div className="container">
        <div className="site-footer__grid">
          <div className="site-footer__col">
            <a className="logo" href={onLanding ? '#top' : '/'} aria-label={t('common.logoTop')}>
              <LogoContent lazy />
            </a>
            <p className="meta">{t('footer.tagline')}</p>
          </div>
          <div className="site-footer__col">
            <span className="site-footer__title">{t('common.language')}</span>
            <LangSwitch style={{ alignSelf: 'flex-start' }} />
          </div>
          <nav className="site-footer__col" aria-label={t('footer.documents')}>
            <span className="site-footer__title">{t('footer.documents')}</span>
            <a href="#terms">{t('footer.terms')}</a>
            <a href="#privacy">{t('footer.privacy')}</a>
          </nav>
          <div className="site-footer__col">
            <span className="site-footer__title">{t('footer.contacts')}</span>
            <a href={`mailto:${siteConfig.supportEmail}`}>{siteConfig.supportEmail}</a>
            <div className="socials">
              <a className="icon-btn" href="#community" aria-label={t('footer.community')}>
                <ChatIcon />
              </a>
              <a className="icon-btn" href="#video" aria-label={t('footer.video')}>
                <VideoIcon />
              </a>
              <a className="icon-btn" href="#feed" aria-label={t('footer.feed')}>
                <FeedIcon />
              </a>
            </div>
          </div>
        </div>
        <div className="site-footer__bottom">
          <span>{t('footer.copyright', { year, studio: siteConfig.studioName })}</span>
          <span>{t('footer.version', { version: siteConfig.version })}</span>
        </div>
      </div>
    </footer>
  );
}
