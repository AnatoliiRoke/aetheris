import { memo } from 'react';
import { images } from '../../assets/images';
import { GameIcon } from '../../components/GameIcon';
import { Glyph } from '../../components/Glyph';
import { LangSwitch } from '../../components/LangSwitch';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useSession } from '../../session/useSession';
import { useTexts } from '../../texts/useTexts';
import { AlertPill, Counter, HeaderTools } from './CivHeaderParts';
import { formatNumber } from './planetText';
import { reportRender } from './renderProbe';
import type { ShellState } from './shellStore';
import { sameJson, useCurrentPlanetId, useShellSelector } from './useShell';

/**
 * What the top bar shows from the shared state: the balances of the currencies with the keys of
 * their names, the energy of the current body and whether a resource of the body fills its
 * storage. Only these numbers are selected, so the bar is drawn again only when one of them changes.
 */
function headerFacts(state: ShellState, planetId: number | null) {
  const entry = planetId === null ? undefined : state.planets.get(planetId);
  // The names come from the reference table of the world; the types of the body name the resources too.
  const nameKeys = new Map([...(entry?.planet.resourceTypes ?? []), ...(state.reference?.resources ?? [])].map((resource) => [resource.id, resource.nameKey]));

  return {
    balances: state.balances.map((balance) => ({
      id: balance.resourceTypeId,
      amount: balance.amount,
      income: balance.incomePerHour,
      nameKey: nameKeys.get(balance.resourceTypeId) ?? null,
    })),
    energy: entry ? { output: entry.planet.energy.output, consumption: entry.planet.energy.consumption, balance: entry.planet.energy.balance } : null,
    storageFull: entry ? entry.planet.resources.some((resource) => resource.isFull) : false,
  };
}

/**
 * The header of the screens of the game (task 00020, section 8.1): the logo, the nickname with the
 * sign of the race, the plan, the counters of the credits, the trade currency and the energy, the
 * three alerts and the buttons of the mail, the settings and the sign-out.
 *
 * Every number comes from the server. What the server has not got yet (the plan, the trade
 * currency, the attack and the battle, the mail) is a placeholder with a dash or a muted button,
 * never a made-up value.
 *
 * A block of its own: it is drawn again only when its own data changes, not with the content of
 * a page or with the tick of a timer of a queue.
 */
export const CivHeader = memo(function CivHeader() {
  reportRender('CivHeader');

  const { t, language } = useI18n();
  const { session } = useSession();
  const { text } = useTexts();
  const planetId = useCurrentPlanetId();
  const civilization = session.status === 'signed-in' ? session.me.civilization : null;
  const raceId = civilization?.raceId ?? null;
  const colorKey = useShellSelector((state) => (raceId === null ? null : state.races?.find((race) => race.id === raceId)?.colorKey ?? null));
  const { balances, energy, storageFull } = useShellSelector((state) => headerFacts(state, planetId), sameJson);

  const nickname = session.status === 'signed-in' ? session.me.nickname : '';
  const raceName = civilization ? text(civilization.raceNameKey) ?? civilization.raceNameKey : '';
  const number = (value: number) => formatNumber(value, language);
  const credits = balances[0] ?? null;
  const creditsName = (credits ? text(credits.nameKey) : undefined) ?? t('economy.credits');

  return (
    <header className="civ-topbar">
      <div className="civ-topbar__id">
        <Link aria-label={t('common.logoHome')} className="brand" to={routes.home}>
          <img alt="" height={28} src={images.logoMark} width={28} />
          <b>AETHERIS</b>
        </Link>
        <span className="nick">
          <Glyph colorKey={colorKey} small />
          {nickname}
          {raceName ? <span className="visually-hidden">{t('civ.raceOf', { race: raceName })}</span> : null}
        </span>
        <span className="chip civ-tariff is-off" title={t('civ.tariffSoon')}>
          <GameIcon name="star" size={12} />
          {t('civ.tariff')}: —
        </span>
      </div>
      <ul aria-label={t('civ.resources')} className="civ-res">
        {balances.length === 0 ? (
          <Counter icon="shapeCoin" label={creditsName} title={creditsName} tone="credits" value={null} />
        ) : (
          balances.map((balance, index) => (
            <Counter
              icon="shapeCoin"
              key={balance.id}
              label={text(balance.nameKey) ?? creditsName}
              title={`${text(balance.nameKey) ?? creditsName}: ${number(balance.amount)} (${t('economy.income')}: ${balance.income > 0 ? '+' : ''}${number(balance.income)}${t('economy.perHourShort')})`}
              tone={index === 0 ? 'credits' : 'trade'}
              value={number(balance.amount)}
            />
          ))
        )}
        {balances.length < 2 ? <Counter icon="shapeTrade" label={t('civ.tradeCurrency')} title={t('civ.tradeCurrencySoon')} tone="trade" value={null} /> : null}
        <Counter
          icon="shapeBolt"
          label={t('civ.energy')}
          title={energy ? t('civ.energyHint', { output: number(energy.output), consumption: number(energy.consumption) }) : t('civ.energy')}
          tone="energy"
          value={energy ? number(energy.balance) : null}
        />
      </ul>
      <div aria-label={t('civ.alerts')} className="civ-topbar__tools" role="group">
        <AlertPill icon="danger" label={t('civ.alert.attack')} title={t('civ.alert.attackSoon')} tone="danger" />
        <AlertPill icon="battle" label={t('civ.alert.battle')} title={t('civ.alert.battleSoon')} tone="danger" />
        {storageFull ? (
          <AlertPill icon="storage" label={t('civ.alert.storageFull')} title={t('civ.alert.storageFullHint')} to={routes.planetStorage} tone="warning" />
        ) : (
          <AlertPill icon="storage" label={t('civ.alert.storageFull')} title={t('civ.alert.storageOk')} tone="warning" />
        )}
        <HeaderTools />
        <LangSwitch />
      </div>
    </header>
  );
});
