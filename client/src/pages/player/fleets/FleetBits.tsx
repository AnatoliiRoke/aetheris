import type { CSSProperties, ReactNode } from 'react';
import { GameIcon } from '../../../components/GameIcon';
import { useTexts } from '../../../texts/useTexts';
import { useFleetsScene } from './useFleetsScene';

/**
 * The round picture of the body: the picture of its type, or the first letter of its name while the
 * type has none.
 *
 * @param planetId The body.
 * @param size The size of the picture: small on the cards, medium in the groups of the free ships, large in the windows.
 */
export function PlanetAvatar({ planetId, size = 'sm' }: { planetId: number; size?: 'sm' | 'md' | 'lg' }) {
  const scene = useFleetsScene();
  const source = scene.imageOf(planetId);

  return source ? (
    <img alt="" className={`fl-avatar fl-avatar--${size}`} loading="lazy" src={source} />
  ) : (
    <span aria-hidden="true" className={`fl-avatar fl-avatar--${size} fl-avatar--letter`}>
      {scene.nameOf(planetId).slice(0, 1)}
    </span>
  );
}

/**
 * The chip of the class of a hull. The class has a tone on the screen, and the chip is never the only
 * carrier of the meaning: it has the name of the class in it.
 *
 * @param nameKey The localisation key of the name of the class.
 * @param suffix What follows the name, for example a count.
 */
export function ClassChip({ nameKey, suffix }: { nameKey: string; suffix?: string }) {
  const { text } = useTexts();
  const scene = useFleetsScene();

  return (
    <span className="fl-class" style={{ '--tone': scene.toneOf(nameKey) } as CSSProperties}>
      {text(nameKey) ?? nameKey}
      {suffix ? ` ${suffix}` : ''}
    </span>
  );
}

/**
 * The pill of the state of a fleet: a sign and a word, in the colour of the state.
 *
 * @param tone Whether the state is good or a warning.
 * @param children The words.
 */
export function StatePill({ tone, children }: { tone: 'ok' | 'warn'; children: ReactNode }) {
  return (
    <span className={`fl-state fl-state--${tone}`}>
      <GameIcon name={tone === 'ok' ? 'check' : 'warn'} size={14} />
      {children}
    </span>
  );
}
