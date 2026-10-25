import { useEffect, useRef, useState } from 'react';
import { GameRenderer } from './game/GameRenderer';
import { serverConfig } from './config/server';

/**
 * Root component.
 *
 * Task 00001 provides an empty canvas and a connection check; the game screens arrive with
 * later tasks.
 */
export default function App() {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const [status, setStatus] = useState<'checking' | 'ok' | 'error'>('checking');

  useEffect(() => {
    const container = containerRef.current;
    if (!container) {
      return;
    }

    const renderer = new GameRendererHolder(container);

    void (async () => {
      await renderer.create();
    })();

    return () => {
      renderer.destroy();
    };
  }, []);

  useEffect(() => {
    const controller = new AbortController();

    void (async () => {
      try {
        const response = await fetch(`${serverConfig.apiBaseUrl}/health`, {
          signal: controller.signal,
        });

        setStatus(response.ok ? 'ok' : 'error');
      } catch {
        if (!controller.signal.aborted) {
          setStatus('error');
        }
      }
    })();

    return () => controller.abort();
  }, []);

  return (
    <div className="app">
      <div ref={containerRef} className="app__canvas" />

      <header className="app__header">
        <h1 className="app__title">AETHERIS</h1>
        <span className={`app__status app__status--${status}`}>
          {status === 'checking' ? 'підключення…' : status === 'ok' ? 'сервер доступний' : 'сервер недоступний'}
        </span>
      </header>
    </div>
  );
}

/**
 * Keeps the PixiJS renderer and its lifecycle together.
 */
class GameRendererHolder {
  private renderer: GameRenderer | null = null;
  private readonly container: HTMLElement;

  constructor(container: HTMLElement) {
    this.container = container;
  }

  async create(): Promise<void> {
    this.renderer = await GameRenderer.create(this.container);
  }

  destroy(): void {
    this.renderer?.destroy();
    this.renderer = null;
  }
}
