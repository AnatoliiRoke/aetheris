import { technologyStatuses, type ResearchTree, type Technology } from '../../api/researchApi';

/**
 * The branches of the tree in the order of the server, each with its technologies; a branch
 * without technologies is kept, so the player sees it is coming.
 *
 * @param tree The tree of the technologies.
 */
export function groupByBranch(tree: ResearchTree | null) {
  if (!tree) {
    return [];
  }

  return tree.branches.map((branch) => ({
    ...branch,
    technologies: tree.technologies
      .filter((technology) => technology.branch === branch.branch)
      .sort((first, second) => first.sortOrder - second.sortOrder),
  }));
}

/**
 * The value of the filter by state that shows every technology.
 */
export const allStatuses = 'all';

/**
 * The chips of the filter by state: the identifier of the chip and the states of the server it
 * keeps. The closed chip holds the technologies the player cannot research yet, the ones with a
 * reason and the ones beyond the reach of the player.
 */
export const statusFilters = [
  { id: 'available', statuses: [technologyStatuses.available] },
  { id: 'queued', statuses: [technologyStatuses.queued] },
  { id: 'researching', statuses: [technologyStatuses.researching] },
  { id: 'learned', statuses: [technologyStatuses.learned] },
  { id: 'closed', statuses: [technologyStatuses.unavailable, technologyStatuses.unknown] },
] as const;

/**
 * Keeps the technologies whose name or description holds the query and whose state is in the
 * chosen chip.
 *
 * @param technologies The technologies of a branch.
 * @param query What the player typed in the search; empty keeps everything.
 * @param filter The identifier of the chosen chip, or {@link allStatuses}.
 * @param textOf The name and the description of a technology, joined.
 */
export function filterTechnologies(technologies: Technology[], query: string, filter: string, textOf: (technology: Technology) => string): Technology[] {
  const needle = query.trim().toLocaleLowerCase();
  const states: readonly string[] | null = statusFilters.find((entry) => entry.id === filter)?.statuses ?? null;

  return technologies.filter(
    (technology) => (states === null || states.includes(technology.status)) && (needle === '' || textOf(technology).toLocaleLowerCase().includes(needle)),
  );
}
