import { Alert } from '../../components/Alert';
import { useI18n } from '../../i18n/useI18n';
import { routes } from '../../router/context';
import { Link } from '../../router/Router';
import { useTexts } from '../../texts/useTexts';
import { ResearchQueue } from './ResearchQueue';
import { useResearch } from './useResearch';
import { useResearchCommands } from './useResearchCommands';
import { useResourceNames } from './useResourceNames';
import { useShellActions } from './useShell';
import { useWorldReference } from './useWorldReference';

/**
 * The research queue on the start screen (task 00012). The column shows the queue only.
 * The tree of the technologies stays on the research screen.
 *
 * The queue is the standard queue of the game (task 00020, section 8.1).
 */
export function ResearchPanel({ planetId }: { planetId: number }) {
  const { t } = useI18n();
  const { text } = useTexts();
  const state = useResearch(planetId);
  const { tree, receivedAt, failed, reload } = state;
  const { busy, notice, cancel } = useResearchCommands(state, planetId);
  const { selectPlanet } = useShellActions();
  const reference = useWorldReference();
  const resourceName = useResourceNames(reference, text);

  if (!tree) {
    return (
      <>
        {notice ? <Alert kind={notice.kind}>{notice.text}</Alert> : null}
        <p className="lead" role="status">
          {failed ? t('research.loadFailed') : t('common.loading')}
        </p>
      </>
    );
  }

  return (
    <>
      {notice ? <Alert kind={notice.kind}>{notice.text}</Alert> : null}
      <ResearchQueue
        action={
          <Link className="btn btn--secondary" onClick={() => selectPlanet(planetId)} to={routes.planetResearch}>
            + {t('civ.addResearch')}
          </Link>
        }
        busy={busy}
        fillClass="f-research"
        onCancel={cancel}
        onFinished={() => void reload()}
        receivedAt={receivedAt}
        resourceName={resourceName}
        technologyName={(key) => text(key) ?? key}
        title={t('player.game.research')}
        tree={tree}
      />
    </>
  );
}
