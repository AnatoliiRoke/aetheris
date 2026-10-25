import { SiteFooter } from '../../components/SiteFooter';
import { SiteHeader } from '../../components/SiteHeader';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import { useI18n } from '../../i18n/useI18n';
import { About } from './About';
import { Faq } from './Faq';
import { Hero } from './Hero';
import { News } from './News';
import { Races } from './Races';
import { Ranking } from './Ranking';
import { Screenshots } from './Screenshots';

/**
 * The first public page of the project (task 00003).
 */
export function LandingPage() {
  const { t } = useI18n();
  useDocumentTitle(t('meta.landing.title'), t('meta.landing.description'));

  return (
    <>
      <a className="skip-link" href="#main">
        {t('common.skip')}
      </a>
      <SiteHeader onLanding />
      <main id="main">
        <Hero />
        <About />
        <Races />
        <Screenshots />
        <Ranking />
        <News />
        <Faq />
      </main>
      <SiteFooter onLanding />
    </>
  );
}
