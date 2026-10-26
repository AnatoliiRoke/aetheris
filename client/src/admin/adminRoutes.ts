/**
 * The addresses of the admin panel (task 00007). The panel is reached only by its address:
 * the menu of a player has no link to it.
 */
export const adminRoutes = {
  home: '/admin',
  generation: '/admin/generation',
  runs: '/admin/runs',
  sectors: '/admin/sectors',
  systems: '/admin/systems',
  objects: '/admin/objects',
  fields: '/admin/fields',
  config: '/admin/config',
  audit: '/admin/audit',
} as const;

/**
 * Whether an address belongs to the admin panel.
 */
export function isAdminPath(path: string): boolean {
  return path === adminRoutes.home || path.startsWith(`${adminRoutes.home}/`);
}
