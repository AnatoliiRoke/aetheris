import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import {
  fakeAuthApi,
  fakeProcessesApi,
  fakeProductionApi,
  renderPage,
  sampleProcessesBuilding,
  signedInMe,
} from '../../../test/renderPage';

/**
 * A signed-in player with a civilization and a confirmed address.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

describe('ProcessesPanel', () => {
  it('shows the recipes of the building and its queue', async () => {
    const processes = fakeProcessesApi();
    renderPage('/planet', { auth: signedIn(), processes });

    const panel = await screen.findByRole('region', { name: 'Процеси' });

    const queue = (await within(panel).findByRole('heading', { name: 'Черга процесів' })).closest('div')!;
    expect(within(queue).getByText('Зроблено 1 з 2')).toBeInTheDocument();

    const recipes = within(panel).getByRole('heading', { name: 'Рецепти процесів' }).closest('div')!;
    expect(within(recipes).getByText('Тераформування планети')).toBeInTheDocument();
    expect(within(recipes).getByText('Розбирання деталі')).toBeInTheDocument();
    expect(within(recipes).getByText('Процес: Тераформінг')).toBeInTheDocument();
    expect(within(recipes).getByText('На одиницю: Залізо 4')).toBeInTheDocument();
  });

  it('keeps a closed recipe inactive and says why', async () => {
    renderPage('/planet', { auth: signedIn() });

    const panel = await screen.findByRole('region', { name: 'Процеси' });
    const closed = (await within(panel).findByText('Закритий')).closest('li')!;

    expect(within(closed).getByText('Потрібно: Адміністративний центр, рівень 2')).toBeInTheDocument();

    // The button stays in sight, inactive, and names the reason.
    const start = within(closed).getByRole('button', { name: 'Почати' });
    expect(start).toBeDisabled();
    expect(start).toHaveAccessibleDescription('Потрібно: Адміністративний центр, рівень 2');
  });

  it('keeps the button inactive when the balance is short, as a hint only', async () => {
    const user = userEvent.setup();
    const processes = fakeProcessesApi();
    renderPage('/planet', { auth: signedIn(), processes });

    const panel = await screen.findByRole('region', { name: 'Процеси' });
    const open = (await within(panel).findByText('Доступний')).closest('li')!;
    const quantity = within(open).getByLabelText('Кількість');

    // Four iron a unit and nine hundred on the body: three hundred units are too many.
    await user.clear(quantity);
    await user.type(quantity, '300');

    const start = within(open).getByRole('button', { name: 'Почати' });
    expect(start).toBeDisabled();
    expect(within(open).getByText(/^Бракує: /)).toBeInTheDocument();
    expect(processes.enqueue).not.toHaveBeenCalled();
  });

  it('offers only the damaged parts of the storage to a repair and sends their condition', async () => {
    const user = userEvent.setup();
    const building = sampleProcessesBuilding();
    const repair = {
      ...building.recipes[1],
      recipeId: 3,
      isAvailable: true,
      reasons: [],
      partSubject: 'damaged' as const,
      ingredients: [],
    };
    const processes = fakeProcessesApi({ building: vi.fn().mockResolvedValue({ ...building, recipes: [repair], queue: [] }) });
    const production = fakeProductionApi({
      stock: vi.fn().mockResolvedValue({
        planetId: 10,
        stacks: [
          { partTypeId: 3, markLevel: 1, hasRaceBonus: false, conditionPercent: 100, quantity: 5, nameKey: 'production.part.part_3.name', imageKey: null },
          { partTypeId: 3, markLevel: 1, hasRaceBonus: false, conditionPercent: 40, quantity: 2, nameKey: 'production.part.part_3.name', imageKey: null },
        ],
        storage: { capacity: 100, stored: 7, fullAt: null },
        gameNow: '2026-10-26T12:00:00Z',
      }),
    });

    renderPage('/planet', { auth: signedIn(), processes, production });

    const panel = await screen.findByRole('region', { name: 'Процеси' });
    const card = (await within(panel).findByText('Доступний')).closest('li')!;
    const select = within(card).getByLabelText('Деталь');

    await waitFor(() => expect(within(select).getAllByRole('option')).toHaveLength(2));
    expect(within(select).getByRole('option', { name: /справність 40%/ })).toBeInTheDocument();
    expect(within(select).queryByRole('option', { name: /справність 100%/ })).toBeNull();

    await user.selectOptions(select, '3:40');
    await user.click(within(card).getByRole('button', { name: 'Почати' }));

    expect(processes.enqueue).toHaveBeenCalledWith(10, 600, 3, 1, {
      sourceResourceTypeId: null,
      targetResourceTypeId: null,
      partTypeId: 3,
      partConditionPercent: 40,
    });
  });

  it('puts an order in the queue and cancels it', async () => {
    const user = userEvent.setup();
    const processes = fakeProcessesApi();
    renderPage('/planet', { auth: signedIn(), processes });

    const panel = await screen.findByRole('region', { name: 'Процеси' });

    const open = (await within(panel).findByText('Доступний')).closest('li')!;
    await user.click(within(open).getByRole('button', { name: 'Почати' }));

    expect(processes.enqueue).toHaveBeenCalledWith(10, 600, 1, 1, {
      sourceResourceTypeId: null,
      targetResourceTypeId: null,
      partTypeId: null,
      partConditionPercent: null,
    });
    expect(await screen.findByText('Замовлення поставлено в чергу.')).toBeInTheDocument();

    await user.click(within(panel).getByRole('button', { name: 'Скасувати' }));

    expect(processes.cancel).toHaveBeenCalledWith(10, 600, 800);
  });

  it('warns when the storage of the parts is full', async () => {
    const production = fakeProductionApi({
      stock: vi.fn().mockResolvedValue({
        planetId: 10,
        stacks: [],
        storage: { capacity: 10, stored: 10, fullAt: null },
        gameNow: '2026-10-26T12:00:00Z',
      }),
    });

    renderPage('/planet', { auth: signedIn(), production });

    const panel = await screen.findByRole('region', { name: 'Процеси' });

    await waitFor(() => expect(within(panel).getByText('Склад заповнений')).toBeInTheDocument());
  });

  it('says that the planet has no building that runs a process', async () => {
    const processes = fakeProcessesApi({ buildings: vi.fn().mockResolvedValue([]) });

    renderPage('/planet', { auth: signedIn(), processes });

    const panel = await screen.findByRole('region', { name: 'Процеси' });

    expect(await within(panel).findByText('На планеті ще немає будівель, що виконують процеси.')).toBeInTheDocument();
    expect(processes.building).not.toHaveBeenCalled();
  });
});
