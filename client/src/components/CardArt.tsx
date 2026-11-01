import { useState } from 'react';
import { images } from '../assets/images';
import { serverConfig } from '../config/server';

/**
 * The picture of a tile of the game (a building, a technology, a recipe): the picture the
 * administrator uploaded for the row, and, while the row has none or the file cannot be read,
 * the one placeholder of the game. The frame has one proportion, so the tiles of a screen stay
 * level whichever of them has a picture; the picture is loaded when it comes near the screen.
 *
 * @param imageKey The key of the picture of the row, or `null` / `undefined` for the placeholder.
 * @param label What the picture shows, for readers of the screen.
 */
export function CardArt({ imageKey, label }: { imageKey?: string | null; label: string }) {
  const [broken, setBroken] = useState<string | null>(null);
  const src = imageKey && broken !== imageKey ? `${serverConfig.apiBaseUrl}/api/v1/images/${encodeURIComponent(imageKey)}` : images.placeholder;
  const isPlaceholder = src === images.placeholder;

  return (
    <div className={isPlaceholder ? 'card-art card-art--placeholder' : 'card-art'}>
      <img alt={isPlaceholder ? '' : label} decoding="async" loading="lazy" src={src} onError={() => setBroken(imageKey ?? null)} />
    </div>
  );
}
