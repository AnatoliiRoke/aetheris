import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ProductionApi } from '../../../api/productionApi';
import {
  fakeAuthApi,
  fakeProductionApi,
  renderPage,
  sampleCharacteristics,
  samplePartCharacteristics,
  signedInMe,
} from '../../../test/renderPage';
import { resetCharacteristicsCache } from '../useCharacteristics';

const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

/**
 * Opens the tab of the modules of the storage and gives the table of them with the row of the
 * hull module of the samples.
 *
 * @param production The server interfaces of the test.
 */
async function openModules(production: ProductionApi) {
  const user = userEvent.setup();

  renderPage('/planet/storage', { auth: signedIn(), production });

  await user.click(await screen.findByRole('tab', { name: 'Модулі' }));

  const table = await screen.findByRole('table', { name: 'Модулі' });
  const row = within(table).getByText('Корпус «Оса» Мк. II').closest('tr')!;

  return { user, table, row };
}

describe('the weight and the characteristics of the parts (task 00027)', () => {
  beforeEach(() => {
    // The catalog of the characteristics is kept for the session, so every case starts with an
    // empty memory and asks the server again.
    resetCharacteristicsCache();
  });

  it('reads the characteristics of the server once and shows them under the row of the part', async () => {
    const production = fakeProductionApi();
    const { user, row } = await openModules(production);

    expect(production.characteristics).toHaveBeenCalledTimes(1);
    expect(production.characteristicParts).toHaveBeenCalledTimes(1);

    await user.click(within(row).getByRole('button', { name: 'Характеристики' }));

    // The names and the units come from the texts of the server, the values from the values of
    // the part.
    expect(await screen.findByText('Тяга')).toBeInTheDocument();
    expect(screen.getByText('120')).toBeInTheDocument();
    expect(screen.getByText('кН')).toBeInTheDocument();
    expect(screen.getByText('Витрата палива')).toBeInTheDocument();
    expect(screen.getByText('3,5')).toBeInTheDocument();
    expect(screen.getByText('т/год')).toBeInTheDocument();
  });

  it('shows the weight of a module in the storage', async () => {
    const { table, row } = await openModules(fakeProductionApi());

    expect(within(table).getByRole('columnheader', { name: 'Вага' })).toBeInTheDocument();
    expect(within(row).getByText('12,5')).toBeInTheDocument();
    expect(within(row).getByText('т')).toBeInTheDocument();
  });

  it('shows a dash for a characteristic the part has no value for', async () => {
    const production = fakeProductionApi({
      characteristicParts: vi.fn().mockResolvedValue([
        { ...samplePartCharacteristics()[0], characteristics: [{ characteristicId: 1, numberValue: 120, levels: {} }] },
      ]),
    });

    const { user, row } = await openModules(production);

    await user.click(within(row).getByRole('button', { name: 'Характеристики' }));

    expect(await screen.findByText('Тяга')).toBeInTheDocument();
    expect(screen.getByText('120')).toBeInTheDocument();
    // The second characteristic of the group is not required, so a part without its value does
    // not show it at all.
    expect(screen.queryByText('Витрата палива')).not.toBeInTheDocument();
  });

  it('keeps the screen whole when the part has no value at all', async () => {
    const production = fakeProductionApi({
      characteristicParts: vi.fn().mockResolvedValue([{ ...samplePartCharacteristics()[0], characteristics: [] }]),
    });

    const { user, row } = await openModules(production);

    await user.click(within(row).getByRole('button', { name: 'Характеристики' }));

    // The characteristic the group requires shows with a dash, never a made-up number.
    // The dash is the one on the plaque: the shell already draws its own dash elsewhere.
    const thrust = await screen.findByText('Тяга');

    expect(within(thrust.closest('li')!).getByText('—')).toBeInTheDocument();
    expect(screen.queryByText('120')).not.toBeInTheDocument();
  });

  it('hides an archived characteristic the part has no value for', async () => {
    const production = fakeProductionApi({
      characteristics: vi.fn().mockResolvedValue([sampleCharacteristics()[0], { ...sampleCharacteristics()[1], isArchived: true }]),
      characteristicParts: vi.fn().mockResolvedValue([
        { ...samplePartCharacteristics()[0], characteristics: [{ characteristicId: 1, numberValue: 120, levels: {} }] },
      ]),
    });

    const { user, row } = await openModules(production);

    await user.click(within(row).getByRole('button', { name: 'Характеристики' }));

    expect(await screen.findByText('Тяга')).toBeInTheDocument();
    expect(screen.queryByText('Витрата палива')).not.toBeInTheDocument();
  });

  it('asks the server for the catalog once for the whole session', async () => {
    const production = fakeProductionApi();
    const first = renderPage('/planet/storage', { auth: signedIn(), production });

    await screen.findByRole('table', { name: 'Модулі' });
    await waitFor(() => expect(production.characteristics).toHaveBeenCalledTimes(1));
    first.unmount();

    renderPage('/planet/storage', { auth: signedIn(), production });

    await screen.findByRole('table', { name: 'Модулі' });
    await waitFor(() => expect(production.characteristics).toHaveBeenCalledTimes(1));
    expect(production.characteristicParts).toHaveBeenCalledTimes(1);
  });
});
