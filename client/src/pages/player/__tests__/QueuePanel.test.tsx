import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { I18nProvider } from '../../../i18n/I18nProvider';
import { PlaceholderBlock } from '../PlaceholderBlock';
import type { QueueEntry } from '../QueueItem';
import { QueuePanel } from '../QueuePanel';

const startMilliseconds = Date.parse('2026-10-26T12:00:00Z');
const secondMilliseconds = 1000;

/**
 * A record of the queue that ends the given number of seconds after the start of the test.
 */
const entry = (overrides: Partial<QueueEntry> = {}): QueueEntry => ({
  id: 7,
  title: 'Шахта заліза, рівень 1',
  underWay: true,
  deadline: startMilliseconds + 3 * secondMilliseconds,
  durationSeconds: 60,
  details: 'Залізо 80',
  cancelLabel: 'Скасувати: Шахта заліза, рівень 1',
  ...overrides,
});

/**
 * Draws the standard queue panel in Ukrainian with the given records.
 */
const renderPanel = (entries: QueueEntry[], overrides: Partial<Parameters<typeof QueuePanel>[0]> = {}) => {
  const onCancel = vi.fn();
  const onFinished = vi.fn();

  render(
    <I18nProvider initialLanguage="uk">
      <QueuePanel
        busy={false}
        emptyText="Черга порожня."
        entries={entries}
        id="test-queue"
        onCancel={onCancel}
        onFinished={onFinished}
        title="Черга будівництва"
        total={2}
        underWayLabel="Будується"
        used={entries.length}
        waitingLabel="Чекає"
        {...overrides}
      />
    </I18nProvider>,
  );

  return { onCancel, onFinished };
};

describe('QueuePanel', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(startMilliseconds);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows the empty state and the free slots', () => {
    renderPanel([]);

    const panel = screen.getByRole('region', { name: 'Черга будівництва' });

    expect(within(panel).getByText('Черга порожня.')).toBeInTheDocument();
    expect(within(panel).getByText('Місць: 0 з 2')).toBeInTheDocument();
    expect(within(panel).getAllByText('Вільне місце')).toHaveLength(2);
    expect(within(panel).queryByRole('timer')).not.toBeInTheDocument();
  });

  it('counts the timer down once a second and fills the progress', () => {
    renderPanel([entry()]);

    expect(screen.getByRole('timer')).toHaveTextContent('Залишилось 0:00:03');
    expect(screen.getByText('Вільне місце')).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(secondMilliseconds);
    });

    expect(screen.getByRole('timer')).toHaveTextContent('Залишилось 0:00:02');
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '97');
  });

  it('tells once that the time ran out', () => {
    const { onFinished } = renderPanel([entry()]);

    act(() => {
      vi.advanceTimersByTime(3 * secondMilliseconds);
    });

    expect(screen.getByText('Завершується…')).toBeInTheDocument();
    expect(onFinished).toHaveBeenCalledTimes(1);

    act(() => {
      vi.advanceTimersByTime(5 * secondMilliseconds);
    });

    expect(onFinished).toHaveBeenCalledTimes(1);
  });

  it('shows a waiting record without a timer and cancels it', async () => {
    vi.useRealTimers();
    const user = userEvent.setup();
    const { onCancel } = renderPanel([entry({ id: 9, title: 'Електростанція, рівень 3', underWay: false, deadline: null, cancelLabel: 'Скасувати: Електростанція, рівень 3' })]);

    expect(screen.getByText(/Чекає/)).toBeInTheDocument();
    expect(screen.queryByRole('timer')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Скасувати: Електростанція, рівень 3' }));

    expect(onCancel).toHaveBeenCalledWith(9);
  });

  it('keeps the cancel inactive while a command runs', () => {
    renderPanel([entry()], { busy: true });

    expect(screen.getByRole('button', { name: 'Скасувати: Шахта заліза, рівень 1' })).toBeDisabled();
  });

  it('shows the way to the choice beside the queue', () => {
    renderPanel([], { action: <a href="/planet/build">Обрати будівлю в чергу</a>, footnote: 'Скасування повертає ресурси.' });

    expect(screen.getByRole('link', { name: 'Обрати будівлю в чергу' })).toHaveAttribute('href', '/planet/build');
    expect(screen.getByText('Скасування повертає ресурси.')).toBeInTheDocument();
  });
});

describe('PlaceholderBlock', () => {
  it('shows the word Soon, the reason and an inactive button, and no numbers', () => {
    render(
      <I18nProvider initialLanguage="uk">
        <PlaceholderBlock reason="Артефактів у грі ще немає." title="Артефакти" />
      </I18nProvider>,
    );

    const block = screen.getByRole('region', { name: 'Артефакти' });

    expect(within(block).getByText('Скоро')).toBeInTheDocument();
    expect(within(block).getByText('Артефактів у грі ще немає.')).toBeInTheDocument();
    expect(within(block).getByRole('button')).toBeDisabled();
    expect(within(block).getByRole('button')).toHaveAttribute('title', 'Артефактів у грі ще немає.');
    expect(block.textContent).not.toMatch(/\d/);
  });
});
