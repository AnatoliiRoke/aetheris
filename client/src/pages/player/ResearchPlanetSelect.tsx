import type { ResearchTree } from '../../api/researchApi';
import { useI18n } from '../../i18n/useI18n';
import { reasonText } from './planetText';

/**
 * The choice of the body a research starts from: the queue is one for the player, and the
 * science centre of the chosen body opens the technologies.
 *
 * @param tree The tree of the technologies with the bodies of the player.
 * @param disabled Whether a command is running.
 * @param onChange What to do when the player picks a body.
 */
export function ResearchPlanetSelect({ tree, disabled, onChange }: { tree: ResearchTree; disabled: boolean; onChange: (planetId: number) => void }) {
  const { t, language } = useI18n();

  return (
    <label className="field">
      <span className="field__label">{t('research.planet')}</span>
      <select className="input" disabled={disabled} value={tree.selectedPlanetId ?? ''} onChange={(event) => onChange(Number(event.target.value))}>
        {tree.planets.map((planet) => (
          <option key={planet.planetId} value={planet.planetId}>
            {planet.isAvailable
              ? t('research.planetOption', { name: planet.name ?? planet.planetId, level: planet.researchCenterLevel })
              : t('research.planetUnavailable', { name: planet.name ?? planet.planetId, reason: reasonText(language, planet.reasonCode ?? '') })}
          </option>
        ))}
      </select>
    </label>
  );
}
