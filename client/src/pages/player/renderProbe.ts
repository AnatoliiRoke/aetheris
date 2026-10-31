/**
 * Lets a test count how many times a component of the frame is drawn. No probe is set in the
 * game, so a report costs nothing there (task 00020, section 7.8).
 */
type RenderProbe = (name: string) => void;

let probe: RenderProbe | null = null;

/**
 * Sets the probe a test counts with, or removes it.
 */
export function setRenderProbe(next: RenderProbe | null): void {
  probe = next;
}

/**
 * Says that a component is being drawn.
 */
export function reportRender(name: string): void {
  probe?.(name);
}
