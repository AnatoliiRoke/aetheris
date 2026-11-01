import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { GameIcon } from '../../../components/GameIcon';
import { useI18n } from '../../../i18n/useI18n';
import { MapZoomContext, type MapZoom } from './mapZoom';
import { centerOn, fitView, screenToWorld, zoomAt, zoomLimits, zoomStep, type Bounds, type MapView, type Viewport } from './mapGeometry';

/** The pixels the arrows move the map by in one press. */
const keyboardPan = 80;

/** The movement of a pointer, in pixels, below which a press is a click and not a drag. */
const dragThreshold = 5;

/** The margin round the world when it is fitted into the area, in pixels. */
const fitMargin = 24;

/** The size the area is taken to have when its real size cannot be measured. */
const fallbackSize: Viewport = { width: 800, height: 560 };

/** How many steps of the scale lie in one doubling of it. */
const stepsPerDoubling = 2;

/** A request to put a place of the world in the middle; a new `token` is a new request. */
export interface MapFocus {
  x: number;
  y: number;
  token: number;
}

/**
 * The area of the map (task 00034): the shift, the zoom (wheel, pinch, buttons, keys), the
 * dragging and the click on an empty place. The drawn world moves by one `transform` of one
 * element and one custom property `--k`, which the marks use to keep their size, so a zoom does
 * not draw the marks again; only a change of the step of the scale does.
 *
 * @param bounds The rectangle of the world that fits the area at the start.
 * @param focus A request to centre the map on a place.
 * @param picking Whether a click points at a target.
 * @param label The name of the area for readers.
 * @param onBackgroundPoint Called with the place of the world when the player clicks an empty place, or presses Enter in the middle of the area while pointing.
 * @param onEscape Called when the player presses Escape.
 * @param controls The extra buttons of the corner of the area.
 * @param overlay What lies over the area and does not move with it (the layers, the legend, the mini-map).
 * @param children The marks and the shapes of the world.
 */
export function MapViewportArea({
  bounds,
  focus,
  picking,
  label,
  onBackgroundPoint,
  onEscape,
  controls,
  overlay,
  onViewChange,
  children,
}: {
  bounds: Bounds;
  focus?: MapFocus | null;
  picking: boolean;
  label: string;
  onBackgroundPoint: (world: { x: number; y: number }, source: 'pointer' | 'keyboard') => void;
  onEscape: () => void;
  controls?: ReactNode;
  overlay?: ReactNode;
  onViewChange?: (view: MapView, viewport: Viewport) => void;
  children: ReactNode;
}) {
  const { t } = useI18n();
  const areaRef = useRef<HTMLDivElement>(null);
  const worldRef = useRef<HTMLDivElement>(null);
  const view = useRef<MapView>({ x: 0, y: 0, k: 1 });
  const size = useRef<Viewport>(fallbackSize);
  const fit = useRef(1);
  const pointers = useRef(new Map<number, { x: number; y: number }>());
  const drag = useRef<{ x: number; y: number; moved: number } | null>(null);
  const pinch = useRef<number | null>(null);
  const [step, setStep] = useState(0);
  const [fitScale, setFitScale] = useState(1);
  const onViewChangeRef = useRef(onViewChange);

  useEffect(() => {
    onViewChangeRef.current = onViewChange;
  });

  const limits = useCallback(() => ({ min: fit.current * zoomLimits.minRelative, max: fit.current * zoomLimits.maxRelative }), []);

  const apply = useCallback((next: MapView) => {
    view.current = next;

    if (worldRef.current) {
      worldRef.current.style.transform = `translate(${next.x}px, ${next.y}px) scale(${next.k})`;
      worldRef.current.style.setProperty('--k', String(next.k));
    }

    const nextStep = Math.round(Math.log2(next.k / fit.current) * stepsPerDoubling);

    setStep((previous) => (previous === nextStep ? previous : nextStep));
    onViewChangeRef.current?.(next, size.current);
  }, []);

  const pixelOf = useCallback((clientX: number, clientY: number) => {
    const rect = areaRef.current?.getBoundingClientRect();

    return { x: clientX - (rect?.left ?? 0), y: clientY - (rect?.top ?? 0) };
  }, []);

  // The world is fitted into the area on the start and when the area changes its size.
  const fitKey = `${bounds.minX}:${bounds.minY}:${bounds.maxX}:${bounds.maxY}`;

  useEffect(() => {
    const area = areaRef.current;

    if (!area) {
      return undefined;
    }

    const measure = () => {
      const rect = area.getBoundingClientRect();

      size.current = rect.width > 0 && rect.height > 0 ? { width: rect.width, height: rect.height } : fallbackSize;
    };
    const refit = () => {
      measure();
      const next = fitView(bounds, size.current, fitMargin);

      fit.current = next.k;
      setFitScale(next.k);
      apply(next);
    };

    refit();

    if (typeof ResizeObserver === 'undefined') {
      return undefined;
    }

    const observer = new ResizeObserver(() => {
      const before = size.current;

      measure();

      // A change of the size keeps the place in the middle of the area where it was.
      if (before.width !== size.current.width || before.height !== size.current.height) {
        const middle = screenToWorld(view.current, { x: before.width / 2, y: before.height / 2 });

        fit.current = fitView(bounds, size.current, fitMargin).k;
        setFitScale(fit.current);
        apply(centerOn(view.current, middle, size.current));
      }
    });

    observer.observe(area);

    return () => observer.disconnect();
    // `bounds` is read through its key: a new object with the same numbers is not a new world.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fitKey, apply]);

  useEffect(() => {
    if (focus) {
      apply(centerOn(view.current, focus, size.current));
    }
    // A new request is a new token.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [focus?.token, apply]);

  // The wheel needs a listener that may stop the scrolling of the page, which a handler of React cannot.
  useEffect(() => {
    const area = areaRef.current;

    if (!area) {
      return undefined;
    }

    const onWheel = (event: WheelEvent) => {
      event.preventDefault();
      apply(zoomAt(view.current, event.deltaY < 0 ? zoomStep : 1 / zoomStep, pixelOf(event.clientX, event.clientY), limits()));
    };

    area.addEventListener('wheel', onWheel, { passive: false });

    return () => area.removeEventListener('wheel', onWheel);
  }, [apply, pixelOf, limits]);

  const zoomBy = (factor: number) => apply(zoomAt(view.current, factor, { x: size.current.width / 2, y: size.current.height / 2 }, limits()));

  const onPointerDown = (event: React.PointerEvent<HTMLDivElement>) => {
    if ((event.target as Element).closest('[data-map-ui]')) {
      return;
    }

    pointers.current.set(event.pointerId, pixelOf(event.clientX, event.clientY));

    if (pointers.current.size === 1) {
      drag.current = { x: event.clientX, y: event.clientY, moved: 0 };
    } else {
      drag.current = null;
      const [first, second] = [...pointers.current.values()];

      pinch.current = first && second ? Math.hypot(first.x - second.x, first.y - second.y) : null;
    }
  };

  const onPointerMove = (event: React.PointerEvent<HTMLDivElement>) => {
    if (!pointers.current.has(event.pointerId)) {
      return;
    }

    const point = pixelOf(event.clientX, event.clientY);
    pointers.current.set(event.pointerId, point);

    if (pointers.current.size >= 2) {
      const [first, second] = [...pointers.current.values()];

      if (first && second && pinch.current) {
        const distance = Math.hypot(first.x - second.x, first.y - second.y);

        apply(zoomAt(view.current, distance / pinch.current, { x: (first.x + second.x) / 2, y: (first.y + second.y) / 2 }, limits()));
        pinch.current = distance;
      }

      return;
    }

    const current = drag.current;

    if (current) {
      const dx = event.clientX - current.x;
      const dy = event.clientY - current.y;

      const wasStill = current.moved < dragThreshold;

      current.moved += Math.abs(dx) + Math.abs(dy);

      // The pointer is captured only once it really drags: a press that stays still must reach the mark under it.
      if (wasStill && current.moved >= dragThreshold) {
        areaRef.current?.setPointerCapture?.(event.pointerId);
      }

      current.x = event.clientX;
      current.y = event.clientY;
      apply({ ...view.current, x: view.current.x + dx, y: view.current.y + dy });
    }
  };

  const onPointerUp = (event: React.PointerEvent<HTMLDivElement>) => {
    const wasClick = pointers.current.size === 1 && drag.current !== null && drag.current.moved < dragThreshold;

    pointers.current.delete(event.pointerId);
    pinch.current = null;
    drag.current = null;

    if (wasClick && !(event.target as Element).closest('[data-map-hit]') && !(event.target as Element).closest('[data-map-ui]')) {
      onBackgroundPoint(screenToWorld(view.current, pixelOf(event.clientX, event.clientY)), 'pointer');
    }
  };

  const onKeyDown = (event: React.KeyboardEvent<HTMLDivElement>) => {
    if (event.target !== areaRef.current) {
      return;
    }

    const pan: Record<string, [number, number]> = {
      ArrowLeft: [keyboardPan, 0],
      ArrowRight: [-keyboardPan, 0],
      ArrowUp: [0, keyboardPan],
      ArrowDown: [0, -keyboardPan],
    };
    const move = pan[event.key];

    if (move) {
      event.preventDefault();
      apply({ ...view.current, x: view.current.x + move[0], y: view.current.y + move[1] });
    } else if (event.key === '+' || event.key === '=') {
      event.preventDefault();
      zoomBy(zoomStep);
    } else if (event.key === '-' || event.key === '_') {
      event.preventDefault();
      zoomBy(1 / zoomStep);
    } else if (event.key === 'Escape') {
      onEscape();
    } else if ((event.key === 'Enter' || event.key === ' ') && picking) {
      event.preventDefault();
      onBackgroundPoint(screenToWorld(view.current, { x: size.current.width / 2, y: size.current.height / 2 }), 'keyboard');
    }
  };

  const zoom = useMemo<MapZoom>(() => ({ scale: fitScale * 2 ** (step / stepsPerDoubling), fit: fitScale }), [step, fitScale]);

  return (
    <div className={picking ? 'map-area is-picking' : 'map-area'}>
      <div
        aria-label={label}
        className="map-area__canvas"
        ref={areaRef}
        role="application"
        tabIndex={0}
        onKeyDown={onKeyDown}
        onPointerCancel={onPointerUp}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={onPointerUp}
      >
        <div className="map-area__stars" aria-hidden="true" />
        <div className="map-world" ref={worldRef}>
          <MapZoomContext.Provider value={zoom}>{children}</MapZoomContext.Provider>
        </div>
        {picking ? (
          <span aria-hidden="true" className="map-area__crosshair">
            <GameIcon name="mapCrosshair" size={28} />
          </span>
        ) : null}
      </div>
      <div className="map-area__overlay" data-map-ui="">
        {overlay}
      </div>
      <div className="map-area__controls" data-map-ui="" role="group" aria-label={t('map.zoomControls')}>
        {controls}
        <button aria-label={t('map.zoomIn')} className="map-round" type="button" onClick={() => zoomBy(zoomStep)}>
          <GameIcon name="mapPlus" size={20} />
        </button>
        <button aria-label={t('map.zoomOut')} className="map-round" type="button" onClick={() => zoomBy(1 / zoomStep)}>
          <GameIcon name="mapMinus" size={20} />
        </button>
      </div>
    </div>
  );
}
