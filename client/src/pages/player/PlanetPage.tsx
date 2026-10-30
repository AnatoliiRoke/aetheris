import { useEffect, useMemo, useRef, useState } from 'react';
import { ApiError } from '../../api/http';
import type { Building, QueueItem } from '../../api/planetApi';
import { useApi } from '../../api/useApi';
import { Alert } from '../../components/Alert';
import { useDocumentTitle } from '../../components/useDocumentTitle';
import { useI18n } from '../../i18n/useI18n';
import { useSession } from '../../session/useSession';
import { useTexts } from '../../texts/useTexts';
import { BuildingCard, EnergyPanel, QueuePanel, ResourcesPanel } from './PlanetPanels';
import { PlayerLayout } from './PlayerLayout';
import { PopulationPanel } from './PopulationPanel';
import { ProcessesPanel } from './ProcessesPanel';
import { ProductionPanel } from './ProductionPanel';
import { TerraformingPanel } from './TerraformingPanel';
import { namer, reasonText, secondsLeft } from './planetText';
import { useNow, usePlanet } from './usePlanet';

type Notice = { kind: 'error' | 'success'; text: string } | null;

/**
 * The home planet of the player (task 00011): the balance and the storage, the energy, the
 * construction queue with its timers and every building with the next level. The page only
 * shows the rules; the server checks them on every command.
 */
export function PlanetPage() {
  const { t, language } = useI18n();
  const { session } = useSession();
  useDocumentTitle(t('meta.planet.title'));

  const civilization = session.status === 'signed-in' ? session.me.civilization : null;
  const { planets } = useApi();
  const { planet, receivedAt, failed, reload, apply } = usePlanet(civilization?.planetId ?? null);
  const now = useNow();
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<Notice>(null);
  const reloadedFor = useRef<number | null>(null);
  const { text } = useTexts();
  const names = useMemo(() => (planet ? namer(planet, language, text) : null), [planet, language, text]);

  // When the timer of the construction under way runs out, the page asks the server again;
  // the read completes a due construction.
  const head = planet?.queue.items[0];
  const headDone = planet && head?.finishesAt ? secondsLeft(planet, head.finishesAt, receivedAt, now) <= 0 : false;

  useEffect(() => {
    if (head && headDone && reloadedFor.current !== head.id) {
      reloadedFor.current = head.id;
      void reload();
    }
  }, [head, headDone, reload]);

  if (!civilization) {
    return null;
  }

  const run = async (action: () => Promise<void>) => {
    setBusy(true);
    setNotice(null);

    try {
      await action();
    } catch (error) {
      const code = error instanceof ApiError ? error.code : 'internal_error';
      setNotice({ kind: 'error', text: code.startsWith('planets_') ? reasonText(language, code) : t('error.generic.text') });
      await reload();
    } finally {
      setBusy(false);
    }
  };

  const onEnqueue = (building: Building) =>
    void run(async () => {
      apply(await planets.enqueue(civilization.planetId, building.buildingTypeId));
      setNotice({ kind: 'success', text: t('planet.queued', { name: names?.building(building.buildingTypeId) ?? building.nameKey, level: building.next.level }) });
    });

  const onCancel = (item: QueueItem) =>
    void run(async () => {
      apply(await planets.cancel(civilization.planetId, item.id));
      setNotice({ kind: 'success', text: t('planet.cancelled') });
    });

  return (
    <PlayerLayout>
      <h1 className="player__title">{t('planet.title', { name: civilization.planetName ?? '' })}</h1>
      {notice ? <Alert kind={notice.kind}>{notice.text}</Alert> : null}
      {failed && !planet ? (
        <div className="panel">
          <Alert kind="error">{t('planet.loadFailed')}</Alert>
          <button className="btn btn--secondary" type="button" onClick={() => void reload()}>
            {t('planet.retry')}
          </button>
        </div>
      ) : null}
      {!planet || !names ? (
        failed ? null : (
          <p className="lead" role="status">
            {t('common.loading')}
          </p>
        )
      ) : (
        <>
          <div className="planet-grid">
            <QueuePanel planet={planet} names={names} receivedAt={receivedAt} now={now} busy={busy} onCancel={onCancel} />
            <EnergyPanel planet={planet} />
          </div>
          <ResourcesPanel planet={planet} names={names} />
          <PopulationPanel planetId={civilization.planetId} />
          <ProductionPanel planetId={civilization.planetId} />
          <ProcessesPanel planetId={civilization.planetId} />
          <TerraformingPanel planetId={civilization.planetId} />
          <section className="panel" aria-labelledby="planet-buildings">
            <h2 className="panel__title" id="planet-buildings">
              {t('planet.buildings')}
            </h2>
            <ul className="building-grid">
              {planet.buildings.map((building) => (
                <BuildingCard key={building.buildingTypeId} building={building} names={names} planet={planet} busy={busy} onEnqueue={onEnqueue} />
              ))}
            </ul>
          </section>
        </>
      )}
    </PlayerLayout>
  );
}
