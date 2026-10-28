import { useCallback, useEffect, useRef, useState } from 'react';
import type { ResearchTree } from '../../api/researchApi';
import { useApi } from '../../api/useApi';

/**
 * How often the tree of the technologies is read again while the page is open, in
 * milliseconds: a research finishes on the server and the client only shows it.
 */
const refreshIntervalMs = 15000;

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
 * Reads the tree of the technologies, keeps it fresh and takes the answers of the commands.
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
    const timer = window.setInterval(() => void reload(), refreshIntervalMs);

    return () => {
      active.current = false;
      window.clearInterval(timer);
    };
  }, [reload]);

  return { tree, receivedAt, failed, reload, apply };
}
