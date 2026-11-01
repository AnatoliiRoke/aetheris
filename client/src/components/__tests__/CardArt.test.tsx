import { fireEvent, render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { images } from '../../assets/images';
import { CardArt } from '../CardArt';

describe('CardArt', () => {
  it('shows the placeholder of the game, hidden from readers, when the row has no picture', () => {
    const { container } = render(<CardArt imageKey={null} label="Шахта" />);
    const image = container.querySelector('img')!;

    expect(image.getAttribute('src')).toBe(images.placeholder);
    expect(image.getAttribute('alt')).toBe('');
    expect(container.querySelector('.card-art--placeholder')).not.toBeNull();
  });

  it('shows the picture of the row by its key, with its name for readers', () => {
    const { container } = render(<CardArt imageKey="abc.webp" label="Шахта" />);
    const image = container.querySelector('img')!;

    expect(image.getAttribute('src')).toContain('/api/v1/images/abc.webp');
    expect(image.getAttribute('alt')).toBe('Шахта');
    expect(container.querySelector('.card-art--placeholder')).toBeNull();
  });

  it('falls back to the placeholder when the file cannot be read', () => {
    const { container } = render(<CardArt imageKey="gone.png" label="Шахта" />);

    fireEvent.error(container.querySelector('img')!);

    expect(container.querySelector('img')!.getAttribute('src')).toBe(images.placeholder);
  });
});
