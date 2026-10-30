import { useMemo } from 'react';
import { images } from '../../assets/images';
import { LangSwitch } from '../../components/LangSwitch';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useSession } from '../../session/useSession';
import { useTexts } from '../../texts/useTexts';
import { formatNumber } from './planetText';
import { useEconomy } from './useEconomy';
import { usePlanet } from './usePlanet';

/**
 * The header of the screens of the game: the logo, the nickname, the resources of the body and
 * the balances of the currencies (task 00020, section 8.1).
 *
 * The header is the same on every screen of the game, so the player sees one place for the
 * resources and the money.
 */
export function CivHeader() {
  const { t, language } = useI18n();
  const { session } = useSession();
  const { balances } = useEconomy(null);
  const { planet } = usePlanet(session.status === 'signed-in' ? session.me.civilization?.planetId ?? null : null);
  const { text } = useTexts();

  // The names of the currencies come from the server with the texts of the game data, so the
  // client knows no currency by name.
  const names = useMemo(() => {
    const byId = new Map((planet?.resourceTypes ?? []).map((resource) => [resource.id, resource.nameKey]));

    return (id: number): string => {
      const key = byId.get(id);

      return key ? text(key) ?? key : String(id);
    };
  }, [planet, text]);

  const nickname = session.status === 'signed-in' ? session.me.nickname : '';

  return (
    <header className="civ-topbar">
      <div className="civ-topbar__id">
        <Link aria-label={t('common.logoHome')} className="brand" to={routes.home}>
          <img alt="" height={28} src={images.logoMark} width={28} />
          <b>AETHERIS</b>
        </Link>
        <span className="nick">
          <span aria-hidden="true" className="glyph glyph--race-1" />
          {nickname}
        </span>
      </div>
      <div className="civ-topbar__res">
        <ul aria-label={t('civ.resources')} className="civ-res">
          {planet?.resources.slice(0, 6).map((resource) => (
            <li key={resource.resourceTypeId}>
              <span className="num">{formatNumber(resource.amount, language)}</span>
            </li>
          ))}
        </ul>
        {balances.length === 0 ? (
          <span className="badge" title={t('economy.credits')}>
            —
          </span>
        ) : (
          <div aria-label={t('economy.credits')} className="civ-balances">
            {balances.map((balance) => (
              <span
                className="badge"
                key={balance.resourceTypeId}
                title={`${names(balance.resourceTypeId)}: ${formatNumber(balance.amount, language)} (${t('economy.income')}: ${balance.incomePerHour > 0 ? '+' : ''}${formatNumber(balance.incomePerHour, language)})`}
              >
                <span>{names(balance.resourceTypeId)}</span> <span className="num">{formatNumber(balance.amount, language)}</span>{' '}
                <span className="meta num">
                  ({balance.incomePerHour > 0 ? '+' : ''}
                  {formatNumber(balance.incomePerHour, language)}
                  {t('economy.perHourShort')})
                </span>
              </span>
            ))}
          </div>
        )}
      </div>
      <div className="civ-topbar__tools">
        <LangSwitch />
      </div>
    </header>
  );
}
