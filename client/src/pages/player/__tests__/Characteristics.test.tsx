import { screen, waitFor, within } from '@testing-library/react';
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
 * Opens the planet screen and gives the row of the hull module of the samples in the stock of
 * the parts, where the weight and the characteristics of a part are drawn.
 *
 * @param production The server interfaces of the test.
 */
async function openStock(production: ProductionApi) {
  renderPage('/planet', { auth: signedIn(), production });

  const row = (await screen.findByText('Корпус «Оса» Мк. II')).closest('li')!;

  return { row };
}

describe('the weight and the characteristics of the parts (task 00027)', () => {
  beforeEach(() => {
    // The catalog of the characteristics is kept for the session, so every case starts with an
    // empty memory and asks the server again.
    resetCharacteristicsCache();
  });

  it('reads the characteristics of the server once and shows them under the row of the part', async () => {
    const production = fakeProductionApi();
    const { row } = await openStock(production);

    // The names and the units come from the texts of the server, the values from the values of
    // the part.
    expect(await within(row).findByText('Тяга')).toBeInTheDocument();
    expect(within(row).getByText('120')).toBeInTheDocument();
    expect(within(row).getByText('кН')).toBeInTheDocument();
    expect(within(row).getByText('Витрата палива')).toBeInTheDocument();
    expect(within(row).getByText('3,5')).toBeInTheDocument();
    expect(within(row).getByText('т/год')).toBeInTheDocument();
    expect(production.characteristics).toHaveBeenCalledTimes(1);
    expect(production.characteristicParts).toHaveBeenCalledTimes(1);
  });

  it('shows the weight of a module in the stock', async () => {
    const { row } = await openStock(fakeProductionApi());

    expect(await within(row).findByText(/12,5/)).toBeInTheDocument();
    expect(within(row).getByText(/12,5/).textContent).toContain('т');
  });

  it('does not show a characteristic the part has no value for', async () => {
    const production = fakeProductionApi({
      characteristicParts: vi.fn().mockResolvedValue([
        { ...samplePartCharacteristics()[0], characteristics: [{ characteristicId: 1, numberValue: 120, levels: {} }] },
      ]),
    });

    const { row } = await openStock(production);

    expect(await within(row).findByText('Тяга')).toBeInTheDocument();
    expect(within(row).getByText('120')).toBeInTheDocument();
    // The second characteristic of the group is not required, so a part without its value does
    // not show it at all.
    expect(within(row).queryByText('Витрата палива')).not.toBeInTheDocument();
  });

  it('keeps the screen whole when the part has no value at all', async () => {
    const production = fakeProductionApi({
      characteristicParts: vi.fn().mockResolvedValue([{ ...samplePartCharacteristics()[0], characteristics: [] }]),
    });

    const { row } = await openStock(production);

    // The characteristic the group requires shows with a dash, never a made-up number.
    const thrust = await within(row).findByText('Тяга');

    expect(within(thrust.closest('li')!).getByText('—')).toBeInTheDocument();
    expect(within(row).queryByText('120')).not.toBeInTheDocument();
  });

  it('hides an archived characteristic the part has no value for', async () => {
    const production = fakeProductionApi({
      characteristics: vi.fn().mockResolvedValue([sampleCharacteristics()[0], { ...sampleCharacteristics()[1], isArchived: true }]),
      characteristicParts: vi.fn().mockResolvedValue([
        { ...samplePartCharacteristics()[0], characteristics: [{ characteristicId: 1, numberValue: 120, levels: {} }] },
      ]),
    });

    const { row } = await openStock(production);

    expect(await within(row).findByText('Тяга')).toBeInTheDocument();
    expect(within(row).queryByText('Витрата палива')).not.toBeInTheDocument();
  });

  it('asks the server for the catalog once for the whole session', async () => {
    const production = fakeProductionApi();
    const first = renderPage('/planet', { auth: signedIn(), production });

    await screen.findByText('Корпус «Оса» Мк. II');
    await waitFor(() => expect(production.characteristics).toHaveBeenCalledTimes(1));
    first.unmount();

    renderPage('/planet', { auth: signedIn(), production });

    await screen.findByText('Корпус «Оса» Мк. II');
    await waitFor(() => expect(production.characteristics).toHaveBeenCalledTimes(1));
    expect(production.characteristicParts).toHaveBeenCalledTimes(1);
  });
});
