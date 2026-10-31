import { useState } from 'react';
import type { ResearchQueueItem, ResearchTree, Technology } from '../../api/researchApi';
import { useApi } from '../../api/useApi';
import { useI18n } from '../../i18n/useI18n';
import { noticeFromError, type Notice } from './screenNotice';
import type { ResearchState } from './useResearch';

/**
 * The commands of the research queue with the message of their result. Every screen that
 * queues or cancels a research does it through this hook, so the queue behaves the same
 * everywhere (task 00020, section 8.1).
 *
 * @param state The tree of the technologies and its answers.
 * @param fallbackPlanetId The body the research starts from when the tree names none.
 */
export function useResearchCommands(state: ResearchState, fallbackPlanetId: number) {
  const { t, language } = useI18n();
  const { research } = useApi();
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<Notice>(null);

  const run = async (action: () => Promise<ResearchTree>, success: string) => {
    setBusy(true);
    setNotice(null);

    try {
      state.apply(await action());
      setNotice({ kind: 'success', text: success });
    } catch (error) {
      setNotice(noticeFromError(error, language, t('error.generic.text')));
      await state.reload();
    } finally {
      setBusy(false);
    }
  };

  const enqueue = (technology: Technology, name: string) =>
    void run(
      () => research.enqueue(state.tree?.selectedPlanetId ?? fallbackPlanetId, technology.id, technology.learnedLevel + 1),
      t('research.queued', { name, level: technology.learnedLevel + 1 }),
    );

  const cancel = (item: ResearchQueueItem) => void run(() => research.cancel(item.id), t('research.cancelled'));

  return { busy, notice, enqueue, cancel };
}
