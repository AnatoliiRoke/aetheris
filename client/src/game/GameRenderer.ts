import { Application } from 'pixi.js';

/**
 * PixiJS application wrapper.
 *
 * The renderer is created once and resized with the window. Game screens are added on
 * top of this layer later; task 00001 only provides the empty canvas.
 */
export class GameRenderer {
  private readonly app: Application;
  private resizeHandler: (() => void) | null = null;

  private constructor(app: Application) {
    this.app = app;
  }

  /**
   * Creates the renderer and attaches it to the given container element.
   */
  static async create(container: HTMLElement): Promise<GameRenderer> {
    const app = new Application();

    await app.init({
      background: '#0b1020',
      resizeTo: container,
      antialias: true,
      autoDensity: true,
      resolution: Math.min(window.devicePixelRatio || 1, 2),
    });

    container.appendChild(app.canvas);

    const renderer = new GameRenderer(app);
    renderer.attachResizeHandling();

    return renderer;
  }

  /**
   * The underlying PixiJS application.
   */
  get application(): Application {
    return this.app;
  }

  /**
   * Stops the render loop, detaches the window listener and destroys the canvas.
   */
  destroy(): void {
    this.detachResizeHandling();
    this.app.destroy(true, { children: true });
  }

  private attachResizeHandling(): void {
    const handleResize = (): void => {
      this.app.renderer.resize(
        this.app.canvas.parentElement?.clientWidth ?? window.innerWidth,
        this.app.canvas.parentElement?.clientHeight ?? window.innerHeight,
      );
    };

    window.addEventListener('resize', handleResize);
    this.resizeHandler = handleResize;
  }

  /**
   * Removes the resize listener from the window.
   *
   * Without this the listener would keep a reference to the destroyed application, so
   * every remount would leak one more listener and the next resize would call into a
   * destroyed renderer.
   */
  private detachResizeHandling(): void {
    if (this.resizeHandler) {
      window.removeEventListener('resize', this.resizeHandler);
      this.resizeHandler = null;
    }
  }
}
