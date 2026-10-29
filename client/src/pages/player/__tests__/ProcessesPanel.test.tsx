import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { fakeAuthApi, fakeProcessesApi, fakeProductionApi, renderPage, signedInMe } from '../../../test/renderPage';

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
    expect(within(closed).queryByRole('button', { name: 'Почати' })).toBeNull();
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
      shipStub: 'ship_on_orbit',
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
