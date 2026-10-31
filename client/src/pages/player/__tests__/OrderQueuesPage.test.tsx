import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { LANGUAGE_STORAGE_KEY } from '../../../i18n/I18nProvider';
import {
  fakeAuthApi,
  fakeProcessesApi,
  fakeProductionApi,
  renderPage,
  sampleProcessOrders,
  sampleProductionOrders,
  signedInMe,
} from '../../../test/renderPage';

/**
 * Task 00020, section 7.5: the production queue and the queue of the processes stand on `/planet`
 * under the research queue, in the standard queue panel, and the old panels of the factory and
 * of the processes do not show the same orders a second time.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

const settleMilliseconds = 50;

const settle = () => act(() => new Promise<void>((resolve) => window.setTimeout(resolve, settleMilliseconds)));

const setVisibility = (state: 'visible' | 'hidden') => {
  vi.spyOn(document, 'visibilityState', 'get').mockReturnValue(state);
  document.dispatchEvent(new Event('visibilitychange'));
};

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
  window.localStorage.removeItem(LANGUAGE_STORAGE_KEY);
});

describe('the queues of the production and of the processes on /planet', () => {
  it('stands under the research queue, in one column with it, in the standard queue panel', async () => {
    renderPage('/planet', { auth: signedIn() });

    // The queue of the research shows a placeholder panel until its tree arrives: wait for the real one.
    await waitFor(() => expect(screen.getByRole('region', { name: 'Черга досліджень' })).toHaveClass('queue-panel'));

    const research = await screen.findByRole('region', { name: 'Черга досліджень' });
    const production = await screen.findByRole('region', { name: 'Черга виробництва' });
    const processes = await screen.findByRole('region', { name: 'Черга процесів' });

    // The same column: one parent holds the three queues, one under the other.
    expect(production.parentElement).toBe(research.parentElement);
    expect(processes.parentElement).toBe(research.parentElement);
    expect(research.compareDocumentPosition(production) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(production.compareDocumentPosition(processes) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

    // The standard queue panel, the same one as the queue of the construction and of the research.
    for (const queue of [research, production, processes, screen.getByRole('region', { name: 'Черга будівництва' })]) {
      expect(queue).toHaveClass('queue-panel');
      expect(queue.querySelector('.queue-list')).not.toBeNull();
    }

    expect(within(production).getByText(/Зроблено 1 з 3/)).toBeInTheDocument();
    expect(within(processes).getByText(/Зроблено 1 з 2/)).toBeInTheDocument();
    expect(within(production).getByText('Місць: 1 з 2')).toBeInTheDocument();
    expect(within(production).getAllByText('Вільне місце')).toHaveLength(1);
  });

  it('keeps the old panels without a second list of the same orders and with the forms they had', async () => {
    renderPage('/planet', { auth: signedIn() });

    const factory = await screen.findByRole('region', { name: 'Виробництво' });
    const processes = await screen.findByRole('region', { name: 'Процеси' });

    await within(factory).findByRole('heading', { name: 'Рецепти' });
    await within(processes).findByRole('heading', { name: 'Рецепти процесів' });

    for (const panel of [factory, processes]) {
      expect(panel.querySelector('.queue-list')).toBeNull();
      expect(panel.querySelector('.queue-item')).toBeNull();
      expect(within(panel).queryByText(/Зроблено/)).not.toBeInTheDocument();
    }

    expect(within(factory).getByRole('button', { name: 'Виробляти' })).toBeInTheDocument();
    expect(within(factory).getByRole('heading', { name: 'Склад деталей' })).toBeInTheDocument();
    expect(within(processes).getAllByRole('button', { name: 'Почати' }).length).toBeGreaterThan(0);
  });

  it('shows the batches of two factories and the orders of two buildings together and names their places', async () => {
    const production = fakeProductionApi({
      orders: vi.fn().mockResolvedValue(
        sampleProductionOrders({
          queueSlots: 4,
          queueUsedSlots: 2,
          orders: [
            sampleProductionOrders().orders[0],
            { ...sampleProductionOrders().orders[0], orderId: 701, planetBuildingId: 501, nameKey: 'production.recipe.recipe_2.name', quantity: 2, producedQuantity: 0, position: 1 },
          ],
        }),
      ),
    });
    const processes = fakeProcessesApi({
      orders: vi.fn().mockResolvedValue(
        sampleProcessOrders({
          queueSlots: 4,
          queueUsedSlots: 2,
          orders: [
            sampleProcessOrders().orders[0],
            { ...sampleProcessOrders().orders[0], orderId: 801, planetBuildingId: 601, nameKey: 'processes.process_recipe.recipe_2.name', quantity: 5, producedQuantity: 2 },
          ],
        }),
      ),
    });
    renderPage('/planet', { auth: signedIn(), production, processes });

    const productionQueue = await screen.findByRole('region', { name: 'Черга виробництва' });
    const processQueue = await screen.findByRole('region', { name: 'Черга процесів' });

    expect(within(productionQueue).getByText('Будматеріали')).toBeInTheDocument();
    expect(within(productionQueue).getByText('Корпусний модуль')).toBeInTheDocument();
    expect(within(productionQueue).getByText('Місць: 2 з 4')).toBeInTheDocument();
    expect(within(productionQueue).getByText(/Зроблено 0 з 2/)).toBeInTheDocument();
    expect(within(processQueue).getByText('Ремонт корабля на орбіті')).toBeInTheDocument();
    expect(within(processQueue).getByText('Розбирання деталі')).toBeInTheDocument();
    expect(within(processQueue).getByText(/Зроблено 2 з 5/)).toBeInTheDocument();
    // One read of the server per queue, however many factories and buildings the body has.
    expect(production.orders).toHaveBeenCalledTimes(1);
    expect(production.orders).toHaveBeenCalledWith(10);
    expect(processes.orders).toHaveBeenCalledWith(10);
  });

  it('cancels a batch of the second factory in its own factory and reads the production again', async () => {
    const user = userEvent.setup();
    const production = fakeProductionApi({
      orders: vi.fn().mockResolvedValue(
        sampleProductionOrders({
          queueUsedSlots: 2,
          orders: [
            sampleProductionOrders().orders[0],
            { ...sampleProductionOrders().orders[0], orderId: 701, planetBuildingId: 501, nameKey: 'production.recipe.recipe_2.name', position: 1 },
          ],
        }),
      ),
    });
    renderPage('/planet', { auth: signedIn(), production });

    const queue = await screen.findByRole('region', { name: 'Черга виробництва' });
    await within(await screen.findByRole('region', { name: 'Виробництво' })).findByRole('heading', { name: 'Рецепти' });
    await settle();
    const ordersBefore = vi.mocked(production.orders).mock.calls.length;
    const factoriesBefore = vi.mocked(production.factories).mock.calls.length;

    await user.click(within(queue).getByRole('button', { name: /Скасувати: Корпусний модуль/ }));

    expect(production.cancel).toHaveBeenCalledWith(10, 501, 701);
    expect(await screen.findByText('Партію скасовано, ресурси повернено.')).toBeInTheDocument();
    // The queue and the panel of the factory read the server again after the command.
    await waitFor(() => expect(vi.mocked(production.orders).mock.calls.length).toBeGreaterThan(ordersBefore));
    await waitFor(() => expect(vi.mocked(production.factories).mock.calls.length).toBeGreaterThan(factoriesBefore));
  });

  it('cancels an order of the processes and reads the processes again', async () => {
    const user = userEvent.setup();
    const processes = fakeProcessesApi();
    renderPage('/planet', { auth: signedIn(), processes });

    const queue = await screen.findByRole('region', { name: 'Черга процесів' });
    await within(await screen.findByRole('region', { name: 'Процеси' })).findByRole('heading', { name: 'Рецепти процесів' });
    await settle();
    const ordersBefore = vi.mocked(processes.orders).mock.calls.length;
    const buildingsBefore = vi.mocked(processes.buildings).mock.calls.length;

    await user.click(within(queue).getByRole('button', { name: /Скасувати: Ремонт корабля на орбіті/ }));

    expect(processes.cancel).toHaveBeenCalledWith(10, 600, 800);
    expect(await screen.findByText('Замовлення скасовано.')).toBeInTheDocument();
    await waitFor(() => expect(vi.mocked(processes.orders).mock.calls.length).toBeGreaterThan(ordersBefore));
    await waitFor(() => expect(vi.mocked(processes.buildings).mock.calls.length).toBeGreaterThan(buildingsBefore));
  });

  it('shows the refusal of the server and reads the queue again when a cancel fails', async () => {
    const user = userEvent.setup();
    const { ApiError } = await import('../../../api/http');
    const production = fakeProductionApi({
      cancel: vi.fn().mockRejectedValue(new ApiError(422, 'production_planet_unknown')),
    });
    renderPage('/planet', { auth: signedIn(), production });

    const queue = await screen.findByRole('region', { name: 'Черга виробництва' });
    const ordersBefore = vi.mocked(production.orders).mock.calls.length;

    await user.click(within(queue).getByRole('button', { name: /Скасувати: Будматеріали/ }));

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    await waitFor(() => expect(vi.mocked(production.orders).mock.calls.length).toBeGreaterThan(ordersBefore));
    expect(within(queue).getByRole('button', { name: /Скасувати: Будматеріали/ })).toBeEnabled();
  });

  it('shows the empty state of both queues with their free places', async () => {
    const production = fakeProductionApi({ orders: vi.fn().mockResolvedValue(sampleProductionOrders({ queueSlots: 3, queueUsedSlots: 0, orders: [] })) });
    const processes = fakeProcessesApi({ orders: vi.fn().mockResolvedValue(sampleProcessOrders({ queueSlots: 2, queueUsedSlots: 0, orders: [] })) });
    renderPage('/planet', { auth: signedIn(), production, processes });

    const productionQueue = await screen.findByRole('region', { name: 'Черга виробництва' });
    const processQueue = await screen.findByRole('region', { name: 'Черга процесів' });

    expect(within(productionQueue).getByText(/У черзі виробництва порожньо/)).toBeInTheDocument();
    expect(within(productionQueue).getAllByText('Вільне місце')).toHaveLength(3);
    expect(within(processQueue).getByText(/У черзі процесів порожньо/)).toBeInTheDocument();
    expect(within(processQueue).getAllByText('Вільне місце')).toHaveLength(2);
  });

  it('says that the queues load, and that they could not, with a way to try again', async () => {
    const user = userEvent.setup();
    let release: (() => void) | undefined;
    const production = fakeProductionApi({
      orders: vi.fn().mockImplementation(() => new Promise((resolve) => {
        release = () => resolve(sampleProductionOrders());
      })),
    });
    const processes = fakeProcessesApi({ orders: vi.fn().mockRejectedValueOnce(new Error('offline')).mockResolvedValue(sampleProcessOrders()) });
    renderPage('/planet', { auth: signedIn(), production, processes });

    const loading = await screen.findByRole('region', { name: 'Черга виробництва' });
    expect(within(loading).getByRole('status')).toHaveTextContent('Завантаження виробництва…');

    const failed = await screen.findByRole('region', { name: 'Черга процесів' });
    expect(await within(failed).findByText('Не вдалося завантажити процеси.')).toBeInTheDocument();

    await user.click(within(failed).getByRole('button', { name: 'Спробувати ще раз' }));
    await waitFor(() => expect(within(screen.getByRole('region', { name: 'Черга процесів' })).getByText(/Зроблено 1 з 2/)).toBeInTheDocument());

    act(() => release?.());
    await waitFor(() => expect(within(screen.getByRole('region', { name: 'Черга виробництва' })).getByText(/Зроблено 1 з 3/)).toBeInTheDocument());
  });

  it('reads both queues again when the tab becomes visible, and not when it becomes hidden', async () => {
    const production = fakeProductionApi();
    const processes = fakeProcessesApi();
    renderPage('/planet', { auth: signedIn(), production, processes });

    await screen.findByRole('region', { name: 'Черга процесів' });
    await settle();
    const productionCalls = vi.mocked(production.orders).mock.calls.length;
    const processCalls = vi.mocked(processes.orders).mock.calls.length;

    act(() => setVisibility('hidden'));
    await settle();
    expect(vi.mocked(production.orders).mock.calls.length).toBe(productionCalls);
    expect(vi.mocked(processes.orders).mock.calls.length).toBe(processCalls);

    act(() => setVisibility('visible'));
    await waitFor(() => expect(vi.mocked(production.orders).mock.calls.length).toBeGreaterThan(productionCalls));
    await waitFor(() => expect(vi.mocked(processes.orders).mock.calls.length).toBeGreaterThan(processCalls));
  });

  it('reads the production again once when the next unit is ready, and never on a timer', async () => {
    vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'Date'] });
    const production = fakeProductionApi({
      orders: vi.fn().mockResolvedValue(
        sampleProductionOrders({
          orders: [{ ...sampleProductionOrders().orders[0], nextUnitAt: '2026-10-26T12:00:30Z' }],
        }),
      ),
    });
    renderPage('/planet', { auth: signedIn(), production });

    const queue = await screen.findByRole('region', { name: 'Черга виробництва' });
    await within(queue).findByRole('timer');
    await settle();
    const callsBefore = vi.mocked(production.orders).mock.calls.length;

    await act(async () => {
      vi.advanceTimersByTime(20_000);
    });
    await settle();
    expect(vi.mocked(production.orders).mock.calls.length).toBe(callsBefore);

    await act(async () => {
      vi.advanceTimersByTime(11_000);
    });
    await settle();
    expect(vi.mocked(production.orders).mock.calls.length).toBe(callsBefore + 1);
  });

  it('speaks English when the player chose English', async () => {
    window.localStorage.setItem(LANGUAGE_STORAGE_KEY, 'en');
    renderPage('/planet', { auth: signedIn() });

    const production = await screen.findByRole('region', { name: 'Production queue' });
    const processes = await screen.findByRole('region', { name: 'Process queue' });

    expect(within(production).getByText(/Made 1 of 3/)).toBeInTheDocument();
    expect(within(processes).getByText(/Made 1 of 2/)).toBeInTheDocument();
    expect(within(production).getByRole('button', { name: /Cancel: Building materials/ })).toBeInTheDocument();
  });
});
