import { SiteFooter } from '../components/SiteFooter';
import { SiteHeader } from '../components/SiteHeader';
import { useDocumentTitle } from '../components/useDocumentTitle';
import { useI18n } from '../i18n/useI18n';
import { routes } from '../router/context';
import { Link } from '../router/Router';

/**
 * Any address the site does not know.
 */
export function NotFoundPage() {
  const { t } = useI18n();
  useDocumentTitle(t('meta.notFound.title'));

  return (
    <>
      <SiteHeader onLanding={false} />
      <main id="main" className="section section--last">
        <div className="container section-head">
          <h1 className="h2">{t('notFound.title')}</h1>
          <p className="lead">{t('notFound.text')}</p>
          <Link className="btn btn--primary btn--lg" to={routes.home}>
            {t('common.home')}
          </Link>
        </div>
      </main>
      <SiteFooter onLanding={false} />
    </>
  );
}
