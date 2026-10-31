import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { I18nProvider } from '../../../i18n/I18nProvider';
import {
  fakeAuthApi,
  fakePlanetApi,
  fakeResearchApi,
  renderPage,
  samplePlanet,
  sampleResearchTree,
  signedInMe,
} from '../../../test/renderPage';
import type { QueueEntry } from '../QueueItem';
import { QueuePanel } from '../QueuePanel';

const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

const secondsToMilliseconds = 1000;

const entry = (overrides: Partial<QueueEntry> = {}): QueueEntry => ({
  id: 1,
  title: 'Шахта, рівень 2',
  underWay: true,
  deadline: Date.now() + 3 * secondsToMilliseconds,
  durationSeconds: 10,
  details: 'Залізо 80',
  cancelLabel: 'Скасувати: Шахта',
  ...overrides,
});

const renderPanel = (entries: QueueEntry[], handlers: { onCancel?: () => void; onFinished?: () => void } = {}) =>
  render(
    <I18nProvider>
      <QueuePanel
        busy={false}
        emptyText="Порожньо"
        entries={entries}
        id="q"
        onCancel={handlers.onCancel ?? (() => undefined)}
        onFinished={handlers.onFinished ?? (() => undefined)}
        title="Черга"
        total={3}
        underWayLabel="Триває"
        used={entries.length}
        waitingLabel="Чекає"
      />
    </I18nProvider>,
  );

afterEach(() => {
  vi.useRealTimers();
});

describe('the standard queue panel (task 00020, section 8.1)', () => {
  it('shows the slots, the record under way, the waiting record and the free slots', () => {
    renderPanel([entry(), entry({ id: 2, title: 'Ферма, рівень 1', underWay: false, deadline: null, durationSeconds: 65 })]);

    const panel = screen.getByRole('region', { name: 'Черга' });
    expect(within(panel).getByText('Місць: 2 з 3')).toBeInTheDocument();
    expect(within(panel).getByText('Шахта, рівень 2')).toBeInTheDocument();
    expect(within(panel).getByText(/Триває/)).toBeInTheDocument();
    expect(within(panel).getByText(/Чекає/)).toBeInTheDocument();
    // A waiting record has no timer yet: it shows how long it takes.
    expect(within(panel).getByText('0:01:05')).toBeInTheDocument();
    expect(within(panel).getAllByRole('timer')).toHaveLength(1);
    expect(within(panel).getAllByText('Вільне місце')).toHaveLength(1);
    expect(within(panel).getAllByRole('button', { name: /Скасувати/ })).toHaveLength(2);
  });

  it('says the queue is empty and keeps every slot free', () => {
    renderPanel([]);

    expect(screen.getByText('Порожньо')).toBeInTheDocument();
    expect(screen.getAllByText('Вільне місце')).toHaveLength(3);
    expect(screen.queryByRole('timer')).not.toBeInTheDocument();
  });

  it('counts the time down every second and tells once when it runs out', () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] });
    const onFinished = vi.fn();
    renderPanel([entry({ deadline: Date.now() + 3 * secondsToMilliseconds })], { onFinished });

    expect(screen.getByRole('timer')).toHaveTextContent('Залишилось 0:00:03');

    act(() => {
      vi.advanceTimersByTime(secondsToMilliseconds);
    });
    expect(screen.getByRole('timer')).toHaveTextContent('Залишилось 0:00:02');
    expect(onFinished).not.toHaveBeenCalled();

    act(() => {
      vi.advanceTimersByTime(2 * secondsToMilliseconds);
    });
    expect(screen.getByText('Завершується…')).toBeInTheDocument();
    expect(onFinished).toHaveBeenCalledTimes(1);

    act(() => {
      vi.advanceTimersByTime(5 * secondsToMilliseconds);
    });
    expect(onFinished).toHaveBeenCalledTimes(1);
  });

  it('cancels the chosen record', async () => {
    const user = userEvent.setup();
    const onCancel = vi.fn();
    renderPanel([entry({ id: 41 })], { onCancel });

    await user.click(screen.getByRole('button', { name: 'Скасувати: Шахта' }));

    expect(onCancel).toHaveBeenCalledWith(41);
  });
});

describe('the queues on the screens of the game', () => {
  const underWayPlanet = () =>
    samplePlanet({
      queue: {
        slotCount: 2,
        items: [
          {
            id: 7,
            buildingTypeId: 5,
            targetLevel: 1,
            position: 1,
            durationSeconds: 60,
            startedAt: '2026-10-26T11:59:30Z',
            finishesAt: '2026-10-26T12:00:30Z',
            costs: [{ resourceTypeId: 1, amount: 80 }],
          },
        ],
      },
    });

  it('shows the construction queue as the same panel on the planet, the construction and the home screens', async () => {
    const panels: string[] = [];

    for (const path of ['/planet', '/planet/build']) {
      const planets = fakePlanetApi({ planet: vi.fn().mockResolvedValue(underWayPlanet()) });
      const { unmount } = renderPage(path, { auth: signedIn(), planets });
      const queue = await screen.findByRole('region', { name: 'Черга будівництва' });

      expect(queue).toHaveClass('queue-panel');
      expect(within(queue).getByText('Шахта заліза, рівень 1')).toBeInTheDocument();
      expect(within(queue).getByText('Місць: 1 з 2')).toBeInTheDocument();
      expect(within(queue).getByRole('timer')).toBeInTheDocument();
      expect(within(queue).getAllByText('Вільне місце')).toHaveLength(1);
      expect(within(queue).getByRole('button', { name: /Скасувати: Шахта заліза, рівень 1/ })).toBeInTheDocument();

      // The structure of the panel, without the timer text and the link of the screen, is the same.
      panels.push(
        [...queue.querySelectorAll('.queue-item')]
          .map((item) => `${item.className}|${item.querySelectorAll('button').length}|${item.querySelectorAll('[role="progressbar"]').length}`)
          .join(';'),
      );
      unmount();
    }

    expect(new Set(panels).size).toBe(1);
  });

  it('shows the research queue as the same panel on the planet and on the research screen', async () => {
    const queued = () =>
      sampleResearchTree({
        queue: [
          {
            id: 9,
            technologyId: 1,
            technologyNameKey: 'research.technology.tech_1.name',
            targetLevel: 1,
            planetId: 10,
            position: 1,
            startsAt: '2026-10-26T12:00:00Z',
            finishesAt: '2026-10-26T12:05:00Z',
            costs: [{ resourceTypeId: 1, amount: 200 }],
          },
        ],
        queueUsedSlots: 1,
      });
    const shapes: string[] = [];

    for (const path of ['/planet', '/planet/research']) {
      const research = fakeResearchApi({ tree: vi.fn().mockResolvedValue(queued()) });
      const { unmount } = renderPage(path, { auth: signedIn(), research });
      const queue = await screen.findByRole('region', { name: 'Черга досліджень' });

      await within(queue).findByText('Глибинний видобуток, рівень 1');
      expect(queue).toHaveClass('queue-panel');
      expect(within(queue).getByText('Місць: 1 з 2')).toBeInTheDocument();
      expect(within(queue).getByText('Залізо 200')).toBeInTheDocument();
      expect(within(queue).getByRole('timer')).toBeInTheDocument();
      expect(within(queue).getAllByText('Вільне місце')).toHaveLength(1);

      shapes.push(
        [...queue.querySelectorAll('.queue-item')]
          .map((item) => `${item.className}|${item.querySelectorAll('button').length}|${item.querySelectorAll('[role="progressbar"]').length}`)
          .join(';'),
      );
      unmount();
    }

    expect(new Set(shapes).size).toBe(1);
  });

  it('cancels a construction from the construction screen and a research from the research screen', async () => {
    const user = userEvent.setup();
    const planets = fakePlanetApi({ planet: vi.fn().mockResolvedValue(underWayPlanet()) });
    const first = renderPage('/planet/build', { auth: signedIn(), planets });

    await user.click(await screen.findByRole('button', { name: /Скасувати: Шахта заліза, рівень 1/ }));
    expect(planets.cancel).toHaveBeenCalledWith(10, 7);
    expect(await screen.findByText('Скасовано, ресурси повернено.')).toBeInTheDocument();
    first.unmount();

    const research = fakeResearchApi({
      tree: vi.fn().mockResolvedValue(
        sampleResearchTree({
          queue: [
            {
              id: 9,
              technologyId: 1,
              technologyNameKey: 'research.technology.tech_1.name',
              targetLevel: 1,
              planetId: 10,
              position: 1,
              startsAt: '2026-10-26T12:00:00Z',
              finishesAt: '2026-10-26T12:05:00Z',
              costs: [{ resourceTypeId: 1, amount: 200 }],
            },
          ],
          queueUsedSlots: 1,
        }),
      ),
    });
    renderPage('/planet/research', { auth: signedIn(), research });

    await user.click(await screen.findByRole('button', { name: /Скасувати: Глибинний видобуток, рівень 1/ }));
    expect(research.cancel).toHaveBeenCalledWith(9);
    expect(await screen.findByText('Дослідження скасовано, ресурси повернено.')).toBeInTheDocument();
  });

  it('asks the server again when the timer of a construction runs out', async () => {
    const planets = fakePlanetApi({
      planet: vi.fn().mockResolvedValue(
        samplePlanet({
          queue: {
            slotCount: 2,
            items: [
              {
                id: 7,
                buildingTypeId: 5,
                targetLevel: 1,
                position: 1,
                durationSeconds: 60,
                startedAt: '2026-10-26T11:59:00Z',
                finishesAt: '2026-10-26T12:00:01Z',
                costs: [],
              },
            ],
          },
        }),
      ),
    });
    renderPage('/planet/build', { auth: signedIn(), planets });

    await screen.findByRole('timer');
    const readsBefore = vi.mocked(planets.planet).mock.calls.length;

    await waitFor(() => expect(vi.mocked(planets.planet).mock.calls.length).toBeGreaterThan(readsBefore), { timeout: 4000 });
  });
});
