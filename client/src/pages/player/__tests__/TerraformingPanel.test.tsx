import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/http';
import { fakeAuthApi, fakeProcessesApi, renderPage, sampleTerraforming, signedInMe } from '../../../test/renderPage';

/**
 * A signed-in player with a civilization and a confirmed address.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

describe('TerraformingPanel', () => {
  it('is an action of its own, apart from the queues of the buildings', async () => {
    const processes = fakeProcessesApi();
    renderPage('/planet', { auth: signedIn(), processes });

    const panel = await screen.findByRole('region', { name: 'Тераформінг' });

    expect(await within(panel).findByRole('option', { name: 'Пристрій тераформінгу Mk I, множник 1,6, на складі 1' })).toBeInTheDocument();
    expect(within(panel).getByRole('option', { name: 'Kepler-4, орбіта 4' })).toBeEnabled();
    // A body terraformed before stays in the list, but it cannot be chosen.
    expect(within(panel).getByRole('option', { name: 'Kepler-5 (вже перетворена)' })).toBeDisabled();
    expect(within(panel).getByText('Множник родовищ після тераформінгу: понад 1,4, до 1,9.')).toBeInTheDocument();

    // The processes panel has no terraforming: the queues stay for the other processes.
    const queues = await screen.findByRole('region', { name: 'Процеси' });
    expect(within(queues).queryByText(/Тераформ/)).not.toBeInTheDocument();
    expect(processes.terraforming).toHaveBeenCalledWith(10);
  });

  it('asks to confirm before the device is spent', async () => {
    const user = userEvent.setup();
    const processes = fakeProcessesApi();
    renderPage('/planet', { auth: signedIn(), processes });

    const panel = await screen.findByRole('region', { name: 'Тераформінг' });
    const start = await within(panel).findByRole('button', { name: 'Запустити пристрій' });
    expect(start).toBeDisabled();

    await user.selectOptions(within(panel).getByLabelText('Пристрій'), '34');
    await user.selectOptions(within(panel).getByLabelText('Вільна планета'), '70');
    await user.click(within(panel).getByRole('button', { name: 'Запустити пристрій' }));

    // Nothing is sent before the confirmation, and the player can step back.
    expect(processes.terraform).not.toHaveBeenCalled();
    expect(within(panel).getByText(/Пристрій тераформінгу Mk I буде витрачено біля планети Kepler-4 безповоротно/)).toBeInTheDocument();
    await user.click(within(panel).getByRole('button', { name: 'Не запускати' }));
    expect(processes.terraform).not.toHaveBeenCalled();

    await user.click(within(panel).getByRole('button', { name: 'Запустити пристрій' }));
    await user.click(within(panel).getByRole('button', { name: 'Підтвердити запуск' }));

    await waitFor(() => expect(processes.terraform).toHaveBeenCalledWith(10, 70, 34));
    expect(await within(panel).findByText('Планету Kepler-4 перетворено: множник родовищ 1,6.')).toBeInTheDocument();
    // The device is spent: the answer of the server has none left.
    expect(within(panel).getAllByText('На складі немає пристроїв тераформінгу').length).toBeGreaterThan(0);
  });

  it('keeps the button inactive without a device or a free body, as a hint only', async () => {
    const processes = fakeProcessesApi({
      terraforming: vi.fn().mockResolvedValue(sampleTerraforming({ devices: [], targets: [] })),
    });
    renderPage('/planet', { auth: signedIn(), processes });

    const panel = await screen.findByRole('region', { name: 'Тераформінг' });
    const start = await within(panel).findByRole('button', { name: 'Запустити пристрій' });

    expect(start).toBeDisabled();
    expect(start).toHaveAccessibleDescription('На складі немає пристроїв тераформінгу Вільних планет у полі зору немає');
  });

  it('shows the reason of the server when the start is refused', async () => {
    const user = userEvent.setup();
    const processes = fakeProcessesApi({
      terraform: vi.fn().mockRejectedValue(new ApiError(422, 'processes_target_colonized')),
    });
    renderPage('/planet', { auth: signedIn(), processes });

    const panel = await screen.findByRole('region', { name: 'Тераформінг' });
    await user.selectOptions(await within(panel).findByLabelText('Пристрій'), '34');
    await user.selectOptions(within(panel).getByLabelText('Вільна планета'), '70');
    await user.click(within(panel).getByRole('button', { name: 'Запустити пристрій' }));
    await user.click(within(panel).getByRole('button', { name: 'Підтвердити запуск' }));

    expect(
      await within(panel).findByText('Планета вже заселена: тераформувати можна лише вільну планету без колонії'),
    ).toBeInTheDocument();
    expect(processes.terraforming).toHaveBeenCalledTimes(2);
  });
});
