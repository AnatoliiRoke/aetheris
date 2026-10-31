import { useCallback, useEffect, useRef, useState } from 'react';
import type { ResearchTree } from '../../api/researchApi';
import { useApi } from '../../api/useApi';
import { useReloadOnVisible } from './tabVisibility';

/**
 * The state of the tree of the technologies on a page: the last answer of the server and the
 * moment it came.
 */
export interface ResearchState {
  tree: ResearchTree | null;
  receivedAt: number;
  failed: boolean;
  reload: () => Promise<void>;
  apply: (tree: ResearchTree) => void;
}

/**
 * Reads the tree of the technologies, reads it again on the events of the page and takes the answers of the commands.
 */
export function useResearch(planetId: number | null): ResearchState {
  const { research } = useApi();
  const [tree, setTree] = useState<ResearchTree | null>(null);
  const [receivedAt, setReceivedAt] = useState(() => Date.now());
  const [failed, setFailed] = useState(false);
  const active = useRef(true);

  const apply = useCallback((next: ResearchTree) => {
    if (active.current) {
      setTree(next);
      setReceivedAt(Date.now());
      setFailed(false);
    }
  }, []);

  const reload = useCallback(async () => {
    if (planetId === null) {
      return;
    }

    try {
      apply(await research.tree(planetId));
    } catch {
      if (active.current) {
        setFailed(true);
      }
    }
  }, [planetId, research, apply]);

  useEffect(() => {
    active.current = true;
    void reload();

    return () => {
      active.current = false;
    };
  }, [reload]);

  useReloadOnVisible(reload);

  return { tree, receivedAt, failed, reload, apply };
}
