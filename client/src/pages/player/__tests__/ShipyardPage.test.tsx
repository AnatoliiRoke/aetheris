import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import {
  fakeAuthApi,
  fakeFleetApi,
  renderPage,
  sampleShipCatalog,
  sampleShipHull,
  sampleShipModule,
  signedInMe,
} from '../../../test/renderPage';

/**
 * A signed-in player with a civilization and a confirmed address.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

/**
 * The catalog of the ships of the test: a combat hull with eight slots and three modules, a
 * drive, a weapon of the combat and a module of another hull.
 */
const catalog = () =>
  sampleShipCatalog({
    hulls: [sampleShipHull({ id: 84, slots: 8, stock: 1 })],
    modules: [
      sampleShipModule({ id: 1, component: 'drive', role: 'universal', energyUse: 0 }),
      sampleShipModule({ id: 2, component: null, role: 'combat', energyUse: 100, compatibleHullIds: [] }),
      sampleShipModule({ id: 3, component: null, role: 'universal', energyUse: 0, compatibleHullIds: [999] }),
    ],
  });

describe('the shipyard screen (task 00038)', () => {
  it('shows the hulls of the catalog and the slots of the chosen one', async () => {
    const user = userEvent.setup();
    renderPage('/shipyard', { auth: signedIn(), fleet: fakeFleetApi({ shipCatalog: vi.fn().mockResolvedValue(catalog()) }) });

    const hulls = await screen.findByRole('region', { name: 'Корпуси' });
    expect(within(hulls).getByRole('heading', { name: 'production.part_type.part_84.name' })).toBeInTheDocument();
    expect(within(hulls).getByText('Слотів: 8')).toBeInTheDocument();

    // The slots appear when the player chooses a hull.
    await user.click(within(hulls).getByRole('heading', { name: 'production.part_type.part_84.name' }));

    const slots = await screen.findByRole('region', { name: 'Слоти корабля' });
    expect(within(slots).getByText('Зайнято 0 з 8 слотів.')).toBeInTheDocument();
    expect(within(slots).getAllByText('Порожній')).toHaveLength(8);
  });

  it('puts a module into a slot and shows the counter of the slots', async () => {
    const user = userEvent.setup();
    renderPage('/shipyard', { auth: signedIn(), fleet: fakeFleetApi({ shipCatalog: vi.fn().mockResolvedValue(catalog()) }) });

    const hulls = await screen.findByRole('region', { name: 'Корпуси' });
    await user.click(within(hulls).getByRole('heading', { name: 'production.part_type.part_84.name' }));

    const modules = await screen.findByRole('region', { name: 'Модулі' });
    const put = within(modules).getAllByRole('button', { name: 'Поставити' });
    await user.click(put[0]);

    const slots = await screen.findByRole('region', { name: 'Слоти корабля' });
    await waitFor(() => expect(within(slots).getByText('Зайнято 1 з 8 слотів.')).toBeInTheDocument());
  });

  it('names the reason why a module does not go into the hull', async () => {
    const user = userEvent.setup();
    renderPage('/shipyard', { auth: signedIn(), fleet: fakeFleetApi({ shipCatalog: vi.fn().mockResolvedValue(catalog()) }) });

    const hulls = await screen.findByRole('region', { name: 'Корпуси' });
    await user.click(within(hulls).getByRole('heading', { name: 'production.part_type.part_84.name' }));

    const modules = await screen.findByRole('region', { name: 'Модулі' });
    expect(within(modules).getByText('Модуль не придатний цьому корпусу')).toBeInTheDocument();
  });

  it('saves the project and tells the player that it is saved', async () => {
    const user = userEvent.setup();
    const fleet = fakeFleetApi({ shipCatalog: vi.fn().mockResolvedValue(catalog()) });
    renderPage('/shipyard', { auth: signedIn(), fleet });

    const hulls = await screen.findByRole('region', { name: 'Корпуси' });
    await user.click(within(hulls).getByRole('heading', { name: 'production.part_type.part_84.name' }));

    const project = await screen.findByRole('region', { name: 'Проєкт' });
    await user.type(within(project).getByLabelText('Назва проєкту'), 'Перехоплювач');
    await user.click(within(project).getByRole('button', { name: 'Зберегти' }));

    expect(await screen.findByText('Проєкт створено.')).toBeInTheDocument();
    expect(fleet.createDesign).toHaveBeenCalledWith({
      planetId: expect.any(Number),
      name: 'Перехоплювач',
      hullPartTypeId: 84,
      hasRaceBonusSlot: false,
    });
  });

  it('shows the projects of the player and their slots', async () => {
    renderPage('/shipyard', { auth: signedIn(), fleet: fakeFleetApi({ shipCatalog: vi.fn().mockResolvedValue(catalog()) }) });

    const projects = await screen.findByRole('region', { name: 'Проєкти' });
    expect(within(projects).getByRole('heading', { name: 'Перехоплювач' })).toBeInTheDocument();
    expect(within(projects).getByText('Зайнято 0 з 8 слотів.')).toBeInTheDocument();
  });
});
