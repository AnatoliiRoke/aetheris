import { useCallback, useMemo, useState, type CSSProperties } from 'react';
import { ApiError } from '../../../api/http';
import type { FinishedShip, ShipHull, ShipyardDesign, ShipyardPart, ShipyardView } from '../../../api/fleetApi';
import type { UnmetRequirement } from '../../../api/planetApi';
import { useApi } from '../../../api/useApi';
import { Alert } from '../../../components/Alert';
import { CardArt } from '../../../components/CardArt';
import { TextField } from '../../../components/FormField';
import { useI18n } from '../../../i18n/useI18n';
import { useTexts } from '../../../texts/useTexts';
import { QueuePanel } from '../QueuePanel';
import type { QueueEntry } from '../QueueItem';
import { deadlineFor, formatDuration, formatNumber, reasonText, requirementText } from '../planetText';
import { noticeFromError, type Notice } from '../screenNotice';
import { AssemblyCosts } from './AssemblyCosts';
import { groupShipsByClass } from './shipyardGroups';
import { needsNormalDrive } from './shipyardParts';

/**
 * The prefixes of the refusals the shipyard names for the player.
 */
const reasonPrefixes = ['fleet_', 'planets_', 'economy_'];

/**
 * The assembly and the dismantling of the shipyard (task 00039). The design tab stays as it was.
 * The numbers, the time and the reasons come from the server.
 *
 * @param mode Which tab is open.
 * @param planetId The body the shipyard stands on.
 * @param yard The shipyard the page already read.
 * @param hulls The hulls of the catalog, so a project shows the picture of its hull.
 * @param receivedAt The real time that read arrived, for the queue clocks.
 * @param failed Whether that read failed before any answer.
 * @param reload Reads the shipyard again.
 */
export function ShipyardWork({
  mode,
  planetId,
  yard,
  hulls,
  receivedAt,
  failed,
  reload,
}: {
  mode: 'assembly' | 'dismantle';
  planetId: number;
  yard: ShipyardView | null;
  hulls: readonly ShipHull[];
  receivedAt: number | null;
  failed: boolean;
  reload: () => Promise<void>;
}) {
  const { t, language } = useI18n();
  const { text, name } = useTexts();
  const { fleet } = useApi();
  const [notice, setNotice] = useState<Notice>(null);
  const [busy, setBusy] = useState(false);

  const run = useCallback(
    async (command: () => Promise<ShipyardView>, done: string) => {
      setBusy(true);
      try {
        await command();
        setNotice({ kind: 'success', text: done });
        await reload();
      } catch (error) {
        setNotice(refusal(error, language, text, t('shipyard.failed')));
      } finally {
        setBusy(false);
      }
    },
    [language, reload, t, text],
  );

  if (failed && !yard) {
    return <Alert kind="error">{t('shipyard.loadFailed')}</Alert>;
  }

  if (!yard || receivedAt === null) {
    return <p className="meta">{t('shipyard.loading')}</p>;
  }

  const multiplier = yard.timeMultiplier > 0 ? yard.timeMultiplier : 1;
  const hasYard = yard.planetBuildingId !== null && yard.level > 0;
  const entries: QueueEntry[] = yard.orders.map((order) => ({
    id: order.id,
    title: order.title,
    underWay: order.underWay,
    deadline: deadlineFor(yard.gameNow, yard.timeMultiplier, order.finishesAt, receivedAt ?? Date.now()),
    durationSeconds: order.durationSeconds / multiplier,
    details: [
      t('shipyard.batch', { made: order.producedQuantity, total: order.quantity }),
      order.costs.map((cost) => `${name(cost.nameKey)} ${formatNumber(cost.amount, language)}`).join(', '),
    ].filter((part) => part !== '').join(' · '),
    cancelLabel: t('shipyard.cancelLabel', { name: order.title }),
  }));

  return (
    <div className="sh-work">
      {notice ? <Alert kind={notice.kind === 'error' ? 'error' : 'info'}>{notice.text}</Alert> : null}
      {hasYard ? null : <p className="sh-note">{t('shipyard.noYard')}</p>}

      {mode === 'assembly' ? (
        <QueuePanel
          busy={busy}
          emptyText={t('shipyard.queueEmpty')}
          entries={entries}
          footnote={t('shipyard.queueNote')}
          id="shipyard-queue"
          onCancel={(orderId) => void run(() => fleet.cancelShipyard(orderId), t('shipyard.cancelled'))}
          onFinished={() => void reload()}
          title={t('shipyard.queueTitle')}
          total={yard.total}
          underWayLabel={t('shipyard.underWay')}
          used={yard.used}
          waitingLabel={t('shipyard.waiting')}
        />
      ) : null}

      {mode === 'assembly' ? (
        yard.designs.length === 0 ? (
          <div className="sh-empty">
            <p className="sh-empty__text">{t('shipyard.noProjects')}</p>
          </div>
        ) : (
          <div className="sh-designs">
            {yard.designs.map((design) => (
              <AssemblyDesignCard
                busy={busy}
                design={design}
                hull={hulls.find((entry) => entry.id === design.hullPartTypeId)}
                key={design.designId}
                multiplier={multiplier}
                onBuild={(quantity) => void run(() => fleet.enqueueShipyard({ planetId, planetBuildingId: yard.planetBuildingId, designId: design.designId, quantity }), t('shipyard.queued'))}
              />
            ))}
          </div>
        )
      ) : null}

      <section aria-labelledby="shipyard-ships" className="panel">
        <div className="panel__head">
          <h2 className="panel__title" id="shipyard-ships">
            {t('shipyard.ships')}
          </h2>
        </div>
        {yard.ships.length === 0 ? (
          <p className="sh-note">{t('shipyard.noShips')}</p>
        ) : (
          <ShipGroups
            busy={busy}
            hasYard={hasYard}
            mode={mode}
            ships={yard.ships}
            onDisassemble={(shipId) => void run(() => fleet.disassembleShip(shipId), t('shipyard.disassembled'))}
          />
        )}
      </section>
    </div>
  );
}

/**
 * The tones the classes of the hulls are drawn in, as the variables of the tokens. A class has no tone of
 * its own in the data, so the classes of the screen take the tones in turn.
 */
const toneVariables = ['--accent-hover', '--group-gem', '--trade', '--group-flame', '--success', '--research'] as const;

/**
 * The finished ships as blocks: the class of the hull, and under it the projects with how many ships
 * of each the player has. The ships are not shown one by one.
 */
function ShipGroups({
  ships,
  mode,
  busy,
  hasYard,
  onDisassemble,
}: {
  ships: readonly FinishedShip[];
  mode: 'assembly' | 'dismantle';
  busy: boolean;
  hasYard: boolean;
  onDisassemble: (shipId: number) => void;
}) {
  const { t, language } = useI18n();
  const { text } = useTexts();
  const groups = useMemo(() => groupShipsByClass(ships), [ships]);
  const tones = useMemo(() => {
    const classes = groups.map((group) => group.classNameKey);

    return new Map(classes.map((key, index) => [key, `var(${toneVariables[index % toneVariables.length]})`]));
  }, [groups]);

  return (
    <div className="sh-groups">
      {groups.map((group) => (
        <section className="sh-group" key={group.classNameKey}>
          <h4 className="sh-group__head">
            <span className="sh-class" style={{ '--tone': tones.get(group.classNameKey) ?? 'var(--text-2)' } as CSSProperties}>
              {text(group.classNameKey) ?? group.classNameKey}
            </span>
          </h4>
          <ul className="sh-projects">
            {group.projects.map((project) => {
              const upkeep = project.ships.reduce((sum, ship) => sum + ship.upkeepPerHour, 0);

              return (
                <li className="sh-project" key={project.designId}>
                  <span className="sh-project__text">
                    <span className="sh-project__name">{project.name}</span>
                    <span className="sh-project__sub">{text(project.hullNameKey) ?? project.hullNameKey}</span>
                  </span>
                  <span className="sh-project__facts">
                    <b className="sh-project__count">{t('fleets.projectCount', { count: project.ships.length })}</b>
                    <span className="sh-project__upkeep">{t('shipyard.upkeep', { amount: formatNumber(upkeep, language) })}</span>
                  </span>
                  {mode === 'dismantle' ? (
                    <span className="sh-project__actions">
                      {project.ships.map((ship) => (
                        <button
                          aria-label={t('shipyard.disassembleLabel', { name: ship.name })}
                          className="btn btn--ghost"
                          disabled={busy || !hasYard || (ship.fleetId ?? null) !== null}
                          key={ship.id}
                          onClick={() => onDisassemble(ship.id)}
                          title={(ship.fleetId ?? null) !== null ? t('shipyard.inFleet') : undefined}
                          type="button"
                        >
                          {t('shipyard.disassemble')}
                        </button>
                      ))}
                    </span>
                  ) : null}
                </li>
              );
            })}
          </ul>
        </section>
      ))}
    </div>
  );
}

/**
 * One project on the assembly tab. The quantity is what the player wants to assemble.
 * The server quote is for one ship; the plaque multiplies the parts and the resources.
 * The time stays the time of one ship, the same way a factory plaque shows one unit.
 */
function AssemblyDesignCard({
  design,
  hull,
  busy,
  multiplier,
  onBuild,
}: {
  design: ShipyardDesign;
  hull: ShipHull | undefined;
  busy: boolean;
  multiplier: number;
  onBuild: (quantity: number) => void;
}) {
  const { t, language } = useI18n();
  const { text, name } = useTexts();
  const [typed, setTyped] = useState('1');
  const quantity = Number(typed);
  const valid = Number.isInteger(quantity) && quantity > 0;
  const shown = valid ? quantity : 0;
  const hullLabel = name(design.hullNameKey);
  const costs = design.costs.map((cost) => ({ ...cost, amount: cost.amount * shown }));
  const parts = design.parts.map((part) => ({ ...part, quantity: part.quantity * shown }));

  return (
    <article className="sh-card">
      <CardArt imageKey={hull?.imageKey} label={hullLabel} />
      <div className="sh-card__head">
        <h3 className="sh-card__title">{design.name}</h3>
      </div>
      <p className="sh-card__hull">{hullLabel}</p>
      <p className="sh-note">
        {t('shipyard.duration')}: {formatDuration(language, design.durationSeconds / multiplier)}
      </p>
      {design.upkeepPerHour !== undefined ? (
        <p className="sh-note">
          {t('shipyard.upkeepLabel')}: {t('fleets.upkeepValue', { amount: formatNumber(design.upkeepPerHour, language) })}
        </p>
      ) : null}
      <AssemblyCosts costs={costs} />
      <p className="sh-note">{t('shipyard.parts')}</p>
      <ul className="sh-parts">
        {parts.map((part) => (
          <li className={part.available < part.quantity ? 'sh-parts__short' : undefined} key={partKey(part)}>
            {t('shipyard.partLine', {
              name: partName(part, name(part.nameKey), part.markLevel > 1 ? t('shipyard.mark', { mark: part.markLevel }) : '', part.hasRaceBonus ? t('shipyard.raceBonus') : ''),
              available: formatNumber(part.available, language),
              quantity: formatNumber(part.quantity, language),
            })}
          </li>
        ))}
      </ul>
      {design.reasonCode ? <p className="sh-note">{designReason(design, language, text)}</p> : null}
      {needsNormalDrive(design) ? <p className="sh-note sh-note--warning">{t('shipyard.ftlNeedsNormalDrive')}</p> : null}
      <div className="sh-card__foot">
        <TextField label={t('shipyard.quantity')} type="number" min={1} step={1} value={typed} disabled={busy} onChange={(event) => setTyped(event.target.value)} />
        <div className="sh-card__actions">
          <button
            className="btn btn--primary"
            disabled={busy || !design.canEnqueue || !valid}
            onClick={() => onBuild(quantity)}
            type="button"
          >
            {t('shipyard.build')}
          </button>
        </div>
      </div>
    </article>
  );
}

function partKey(part: ShipyardPart): string {
  return `${part.partTypeId}-${part.markLevel}-${part.hasRaceBonus ? 1 : 0}-${part.isHull ? 1 : 0}`;
}

function partName(part: ShipyardPart, label: string, mark: string, bonus: string): string {
  const markText = mark === '' ? '' : ` ${mark}`;
  const bonusText = part.hasRaceBonus && bonus !== '' ? ` ${bonus}` : '';

  return `${label}${markText}${bonusText}`;
}

function designReason(design: ShipyardDesign, language: Parameters<typeof requirementText>[0], entities: Parameters<typeof requirementText>[2]): string {
  if (design.requirementNameKey) {
    const unmet: UnmetRequirement = {
      requirementId: 0,
      requirementType: 'building_level',
      targetId: design.hullPartTypeId,
      targetLevel: design.requirementLevel ?? 0,
      reasonCode: design.reasonCode ?? '',
      targetNameKey: design.requirementNameKey,
      isClosed: true,
    };

    return requirementText(language, unmet, entities);
  }

  return reasonText(language, design.reasonCode ?? '');
}

function refusal(error: unknown, language: Parameters<typeof requirementText>[0], entities: Parameters<typeof requirementText>[2], generic: string): Notice {
  if (error instanceof ApiError && error.code === 'planets_requirement_building_level') {
    const nameKey = detailString(error.details, 'requirement_name_key', 'requirementNameKey');

    if (nameKey) {
      return {
        kind: 'error',
        text: requirementText(language, {
          requirementId: 0,
          requirementType: 'building_level',
          targetId: 0,
          targetLevel: detailNumber(error.details, 'requirement_level', 'requirementLevel'),
          reasonCode: error.code,
          targetNameKey: nameKey,
          isClosed: true,
        }, entities),
      };
    }
  }

  return noticeFromError(error, language, generic, reasonPrefixes);
}

function detailString(details: Record<string, unknown>, snakeName: string, camelName: string): string | null {
  const fieldText = details[snakeName] ?? details[camelName];

  return typeof fieldText === 'string' && fieldText !== '' ? fieldText : null;
}

function detailNumber(details: Record<string, unknown>, snakeName: string, camelName: string): number {
  const fieldNumber = details[snakeName] ?? details[camelName];

  return typeof fieldNumber === 'number' ? fieldNumber : 0;
}
