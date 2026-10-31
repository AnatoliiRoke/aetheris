import { useMemo, useState } from 'react';
import type { Building, QueueItem } from '../../api/planetApi';
import { useApi } from '../../api/useApi';
import { useI18n } from '../../i18n/useI18n';
import { useSession } from '../../session/useSession';
import { useTexts } from '../../texts/useTexts';
import { namer } from './planetText';
import { noticeFromError, type Notice } from './screenNotice';
import { usePlanet } from './usePlanet';
import { useCurrentPlanetId } from './useShell';

/**
 * What a screen of a body needs: the body, its names, and the commands of the construction queue
 * with the message of their result. The screens of the planet and of the construction read the
 * body and send the commands through this one hook, so the queue behaves the same on both
 * (task 00020, section 8.1).
 */
export function usePlanetScreen() {
  const { t, language } = useI18n();
  const { session } = useSession();
  const { planets } = useApi();
  const { text } = useTexts();
  const civilization = session.status === 'signed-in' ? session.me.civilization : null;
  const planetId = useCurrentPlanetId();
  const { planet, receivedAt, failed, reload, apply } = usePlanet(planetId);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<Notice>(null);
  const names = useMemo(() => (planet ? namer(planet, language, text) : null), [planet, language, text]);

  const run = async (action: () => Promise<void>) => {
    setBusy(true);
    setNotice(null);

    try {
      await action();
    } catch (error) {
      setNotice(noticeFromError(error, language, t('error.generic.text')));
      await reload();
    } finally {
      setBusy(false);
    }
  };

  const onEnqueue = (building: Building) =>
    void run(async () => {
      if (planetId === null) {
        return;
      }

      apply(await planets.enqueue(planetId, building.buildingTypeId));
      setNotice({ kind: 'success', text: t('planet.queued', { name: names?.building(building.buildingTypeId) ?? building.nameKey, level: building.next.level }) });
    });

  const onCancel = (item: QueueItem) =>
    void run(async () => {
      if (planetId === null) {
        return;
      }

      apply(await planets.cancel(planetId, item.id));
      setNotice({ kind: 'success', text: t('planet.cancelled') });
    });

  return { civilization, planetId, planet, receivedAt, failed, reload, names, busy, notice, onEnqueue, onCancel };
}
