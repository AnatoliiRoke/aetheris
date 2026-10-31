import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { ActiveOrder, ActiveOrders } from '../../../api/activeOrders';
import type { ProcessActiveOrder } from '../../../api/processesApi';
import { I18nProvider } from '../../../i18n/I18nProvider';
import { TextsContext, type TextsValue } from '../../../texts/context';
import { sampleProcessOrders, sampleProductionOrders } from '../../../test/renderPage';
import { ProcessQueue } from '../ProcessQueue';
import { ProductionQueue } from '../ProductionQueue';

/**
 * Task 00020, section 7.5: the production queue and the queue of the processes in the standard
 * queue panel. The queues are drawn on their own here, with the names of the game data given by
 * the test; the page that holds them is tested in `OrderQueuesPage.test.tsx`.
 */
const startMilliseconds = Date.parse('2026-10-26T12:00:00Z');
const secondMilliseconds = 1000;

const names: Record<string, string> = {
  'production.recipe.recipe_1.name': 'Будматеріали',
  'production.recipe.recipe_2.name': 'Корпусний модуль',
  'processes.process_recipe.recipe_1.name': 'Розбирання деталі',
  'processes.process_recipe.recipe_2.name': 'Конвертація металів',
  'processes.process_type.recycling.name': 'Розбирання',
  'processes.process_type.matter_conversion.name': 'Конвертація',
  'planets.building.building_13.name': 'Завод',
  'planets.building.building_14.name': 'Верф',
  'planets.building.building_20.name': 'Фабрика утилізації',
  'planets.building.building_21.name': 'Конвертер',
};

const texts: TextsValue = {
  text: (key) => (key ? names[key] : undefined),
  name: (key, fallback) => (key ? (names[key] ?? fallback ?? key) : (fallback ?? '')),
  reload: () => undefined,
};

/**
 * Draws a queue with the names of the game data, in Ukrainian.
 */
const draw = (queue: ReactNode) =>
  render(
    <I18nProvider initialLanguage="uk">
      <TextsContext.Provider value={texts}>{queue}</TextsContext.Provider>
    </I18nProvider>,
  );

/**
 * A batch of a factory that makes its next unit the given number of game seconds after the start.
 */
const batch = (overrides: Partial<ActiveOrder> = {}): ActiveOrder => ({
  ...sampleProductionOrders().orders[0],
  nextUnitAt: new Date(startMilliseconds + 90 * secondMilliseconds).toISOString(),
  unitSeconds: 120,
  ...overrides,
});

const productionOrders = (orders: ActiveOrder[], overrides: Partial<ActiveOrders> = {}): ActiveOrders => ({
  ...sampleProductionOrders(),
  queueSlots: 4,
  queueUsedSlots: orders.length,
  orders,
  ...overrides,
});

const processOrders = (orders: ProcessActiveOrder[], overrides: Partial<ActiveOrders<ProcessActiveOrder>> = {}): ActiveOrders<ProcessActiveOrder> => ({
  ...sampleProcessOrders(),
  queueSlots: 4,
  queueUsedSlots: orders.length,
  orders,
  ...overrides,
});

const drawProduction = (orders: ActiveOrders, overrides: Partial<Parameters<typeof ProductionQueue>[0]> = {}) => {
  const onCancel = vi.fn();
  const onFinished = vi.fn();

  draw(
    <ProductionQueue
      busy={false}
      onCancel={onCancel}
      onFinished={onFinished}
      orders={orders}
      receivedAt={startMilliseconds}
      timeMultiplier={1}
      {...overrides}
    />,
  );

  return { onCancel, onFinished };
};

describe('ProductionQueue: the batches of all the factories in one standard queue', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(startMilliseconds);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('puts the batches of several factories in one list with the units made out of the whole batch', () => {
    drawProduction(
      productionOrders([
        batch({ orderId: 700, planetBuildingId: 500, buildingNameKey: 'planets.building.building_13.name', producedQuantity: 1, quantity: 3 }),
        batch({
          orderId: 701,
          planetBuildingId: 501,
          buildingNameKey: 'planets.building.building_14.name',
          nameKey: 'production.recipe.recipe_2.name',
          producedQuantity: 0,
          quantity: 2,
        }),
        batch({ orderId: 702, planetBuildingId: 500, position: 2, startsAt: null, nextUnitAt: null, finishesAt: null, producedQuantity: 0, quantity: 5 }),
      ]),
    );

    const queue = screen.getByRole('region', { name: 'Черга виробництва' });
    const records = within(queue).getAllByRole('listitem');

    // Three batches of two factories, and the one free place of the four.
    expect(records).toHaveLength(4);
    expect(within(queue).getByText('Місць: 3 з 4')).toBeInTheDocument();
    expect(within(queue).getAllByText('Вільне місце')).toHaveLength(1);

    expect(within(records[0]).getByText('Будматеріали')).toBeInTheDocument();
    expect(within(records[0]).getByText('Зроблено 1 з 3 · Завод')).toBeInTheDocument();
    expect(within(records[1]).getByText('Корпусний модуль')).toBeInTheDocument();
    expect(within(records[1]).getByText('Зроблено 0 з 2 · Верф')).toBeInTheDocument();
    expect(within(records[2]).getByText('Зроблено 0 з 5 · Завод')).toBeInTheDocument();
  });

  it('counts down to the next unit once a second and fills the progress of the unit', () => {
    drawProduction(productionOrders([batch()]));

    const queue = screen.getByRole('region', { name: 'Черга виробництва' });

    expect(within(queue).getByText(/Виробляється одиниця/)).toBeInTheDocument();
    expect(within(queue).getByRole('timer')).toHaveTextContent('Залишилось 0:01:30');
    expect(within(queue).getByRole('progressbar')).toHaveAttribute('aria-valuenow', '25');

    act(() => {
      vi.advanceTimersByTime(secondMilliseconds);
    });

    expect(within(queue).getByRole('timer')).toHaveTextContent('Залишилось 0:01:29');
    expect(within(queue).getByRole('progressbar')).toHaveAttribute('aria-valuenow', '26');
  });

  it('counts the time of the body: a multiplier of two halves the real time', () => {
    drawProduction(productionOrders([batch({ nextUnitAt: new Date(startMilliseconds + 120 * secondMilliseconds).toISOString() })]), { timeMultiplier: 2 });

    expect(screen.getByRole('timer')).toHaveTextContent('Залишилось 0:01:00');
  });

  it('tells once when the time of the unit runs out, so the page asks the server', () => {
    const { onFinished } = drawProduction(productionOrders([batch()]));

    act(() => {
      vi.advanceTimersByTime(90 * secondMilliseconds);
    });

    expect(screen.getByText('Завершується…')).toBeInTheDocument();
    expect(onFinished).toHaveBeenCalledTimes(1);

    act(() => {
      vi.advanceTimersByTime(10 * secondMilliseconds);
    });

    expect(onFinished).toHaveBeenCalledTimes(1);
  });

  it('gives a waiting batch no timer and a batch that waits for the storage no timer and no reading', () => {
    const { onFinished } = drawProduction(
      productionOrders([
        batch({ orderId: 700, isBlocked: true, nextUnitAt: new Date(startMilliseconds - 5 * secondMilliseconds).toISOString() }),
        batch({ orderId: 701, startsAt: null, nextUnitAt: null, finishesAt: null }),
      ]),
    );

    const queue = screen.getByRole('region', { name: 'Черга виробництва' });

    expect(within(queue).getByText(/Чекає вільного місця на складі/)).toBeInTheDocument();
    expect(within(queue).getByText(/Очікує/)).toBeInTheDocument();
    expect(within(queue).queryByRole('timer')).not.toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(60 * secondMilliseconds);
    });

    expect(onFinished).not.toHaveBeenCalled();
  });

  it('cancels the batch in its own factory', async () => {
    vi.useRealTimers();
    const user = userEvent.setup();
    const second = batch({ orderId: 701, planetBuildingId: 501, nameKey: 'production.recipe.recipe_2.name' });
    const { onCancel } = drawProduction(productionOrders([batch({ orderId: 700 }), second]));

    await user.click(screen.getByRole('button', { name: /Скасувати: Корпусний модуль/ }));

    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(onCancel).toHaveBeenCalledWith(second);
  });

  it('keeps the cancel inactive while a command runs', () => {
    drawProduction(productionOrders([batch()]), { busy: true });

    expect(screen.getByRole('button', { name: /Скасувати: Будматеріали/ })).toBeDisabled();
  });

  it('says that nothing is made and shows all the free places', () => {
    drawProduction(productionOrders([], { queueSlots: 3 }));

    const queue = screen.getByRole('region', { name: 'Черга виробництва' });

    expect(within(queue).getByText(/У черзі виробництва порожньо/)).toBeInTheDocument();
    expect(within(queue).getByText('Місць: 0 з 3')).toBeInTheDocument();
    expect(within(queue).getAllByText('Вільне місце')).toHaveLength(3);
  });

  it('falls back to the number of the recipe when the server names none', () => {
    drawProduction(productionOrders([batch({ nameKey: '', recipeId: 77 })]));

    expect(screen.getByText('77')).toBeInTheDocument();
  });
});

describe('ProcessQueue: the orders of all the buildings in one standard queue', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(startMilliseconds);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  const order = (overrides: Partial<ProcessActiveOrder> = {}): ProcessActiveOrder => ({
    ...sampleProcessOrders().orders[0],
    nameKey: 'processes.process_recipe.recipe_1.name',
    processTypeNameKey: 'processes.process_type.recycling.name',
    nextUnitAt: new Date(startMilliseconds + 30 * secondMilliseconds).toISOString(),
    unitSeconds: 60,
    ...overrides,
  });

  const drawProcesses = (orders: ActiveOrders<ProcessActiveOrder>) => {
    const onCancel = vi.fn();
    const onFinished = vi.fn();

    draw(
      <ProcessQueue
        busy={false}
        onCancel={onCancel}
        onFinished={onFinished}
        orders={orders}
        receivedAt={startMilliseconds}
        timeMultiplier={1}
      />,
    );

    return { onCancel, onFinished };
  };

  it('shows every order the server sends, of every kind, in the same form as the production', () => {
    drawProcesses(
      processOrders([
        order({ orderId: 800, planetBuildingId: 600, buildingNameKey: 'planets.building.building_20.name', producedQuantity: 1, quantity: 2 }),
        order({
          orderId: 801,
          planetBuildingId: 601,
          buildingNameKey: 'planets.building.building_21.name',
          nameKey: 'processes.process_recipe.recipe_2.name',
          processTypeNameKey: 'processes.process_type.matter_conversion.name',
          producedQuantity: 3,
          quantity: 10,
        }),
      ]),
    );

    const queue = screen.getByRole('region', { name: 'Черга процесів' });
    const records = within(queue).getAllByRole('listitem');

    expect(records).toHaveLength(4);
    expect(within(queue).getByText('Місць: 2 з 4')).toBeInTheDocument();
    expect(within(records[0]).getByText('Розбирання деталі')).toBeInTheDocument();
    expect(within(records[0]).getByText('Розбирання · Зроблено 1 з 2 · Фабрика утилізації')).toBeInTheDocument();
    expect(within(records[1]).getByText('Конвертація металів')).toBeInTheDocument();
    expect(within(records[1]).getByText('Конвертація · Зроблено 3 з 10 · Конвертер')).toBeInTheDocument();
  });

  it('counts down to the next unit and tells once when it is ready', () => {
    const { onFinished } = drawProcesses(processOrders([order()]));

    expect(screen.getByText(/Виконується одиниця/)).toBeInTheDocument();
    expect(screen.getByRole('timer')).toHaveTextContent('Залишилось 0:00:30');

    act(() => {
      vi.advanceTimersByTime(30 * secondMilliseconds);
    });

    expect(onFinished).toHaveBeenCalledTimes(1);
  });

  it('cancels the order in its own building', async () => {
    vi.useRealTimers();
    const user = userEvent.setup();
    const target = order({ orderId: 801, planetBuildingId: 601 });
    const { onCancel } = drawProcesses(processOrders([order({ orderId: 800 }), target]));

    await user.click(within(screen.getAllByRole('listitem')[1]).getByRole('button', { name: /Скасувати: Розбирання деталі/ }));

    expect(onCancel).toHaveBeenCalledWith(target);
  });

  it('says that nothing runs and shows the free places', () => {
    drawProcesses(processOrders([], { queueSlots: 2 }));

    const queue = screen.getByRole('region', { name: 'Черга процесів' });

    expect(within(queue).getByText(/У черзі процесів порожньо/)).toBeInTheDocument();
    expect(within(queue).getAllByText('Вільне місце')).toHaveLength(2);
  });
});
