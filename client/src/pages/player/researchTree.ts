import type { ResearchTree, Technology } from '../../api/researchApi';

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
 * Keeps the technologies whose name or description holds the query and whose state is the
 * chosen one.
 *
 * @param technologies The technologies of a branch.
 * @param query What the player typed in the search; empty keeps everything.
 * @param status The chosen state, or {@link allStatuses}.
 * @param textOf The name and the description of a technology, joined.
 */
export function filterTechnologies(technologies: Technology[], query: string, status: string, textOf: (technology: Technology) => string): Technology[] {
  const needle = query.trim().toLocaleLowerCase();

  return technologies.filter(
    (technology) => (status === allStatuses || technology.status === status) && (needle === '' || textOf(technology).toLocaleLowerCase().includes(needle)),
  );
}
