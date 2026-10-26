import type { GenerationRun } from './adminApi';
import type { AdminMessageKey } from './i18n/uk';

const classKeys: Record<string, AdminMessageKey> = {
  planet: 'admin.class.planet',
  gas_giant: 'admin.class.gasGiant',
  asteroid_field: 'admin.class.asteroidField',
  star: 'admin.class.star',
  nebula: 'admin.class.nebula',
};

/**
 * The name of a class of celestial objects; an unknown class shows its code.
 */
export function classLabel(t: (key: AdminMessageKey) => string, objectClass: string): string {
  const key = classKeys[objectClass];
  return key ? t(key) : objectClass;
}

const statusKeys: Record<GenerationRun['status'], AdminMessageKey> = {
  queued: 'admin.run.queued',
  running: 'admin.run.running',
  succeeded: 'admin.run.succeeded',
  failed: 'admin.run.failed',
};

/**
 * The name of the status of a generation run.
 */
export function statusLabel(t: (key: AdminMessageKey) => string, status: GenerationRun['status']): string {
  return t(statusKeys[status]);
}
