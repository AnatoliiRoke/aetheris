import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/http';
import { fakeAuthApi, fakeProcessesApi, renderPage, sampleTerraforming, sampleTerraformingTarget, signedInMe } from '../../../test/renderPage';

/**
 * A signed-in player with a civilization and a confirmed address.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

describe('TerraformingPanel', () => {
  it('is an action of its own, apart from the queues of the buildings', async () => {
    const user = userEvent.setup();
    const processes = fakeProcessesApi();
    renderPage('/planet', { auth: signedIn(), processes });

    const panel = await screen.findByRole('region', { name: 'Тераформінг' });

    expect(await within(panel).findByRole('option', { name: 'Kepler-4, орбіта 4' })).toBeEnabled();
    // A body terraformed before stays in the list, but it cannot be chosen.
    expect(within(panel).getByRole('option', { name: 'Kepler-5 (вже перетворена)' })).toBeDisabled();
    expect(
      within(panel).getByText('Кожне родовище планети отримає власний випадковий множник від 1,4 до 1,9; родовища, що вже багатші, не змінюються.'),
    ).toBeInTheDocument();

    // The device is in the hold of a ship by the chosen body, never in the storage of the colony.
    expect(within(panel).getByRole('option', { name: 'Спершу обери планету' })).toBeInTheDocument();
    await user.selectOptions(within(panel).getByLabelText('Вільна планета'), '70');
    expect(within(panel).getByRole('option', { name: 'Корабель №5: Пристрій тераформінгу Mk I, у трюмі 2' })).toBeInTheDocument();

    // The processes panel has no terraforming: the queues stay for the other processes.
    const queues = await screen.findByRole('region', { name: 'Процеси' });
    expect(within(queues).queryByText(/Тераформ/)).not.toBeInTheDocument();
    expect(processes.terraforming).toHaveBeenCalledWith();
  });

  it('asks to confirm before the device is spent', async () => {
    const user = userEvent.setup();
    const processes = fakeProcessesApi();
    renderPage('/planet', { auth: signedIn(), processes });

    const panel = await screen.findByRole('region', { name: 'Тераформінг' });
    const start = await within(panel).findByRole('button', { name: 'Запустити пристрій' });
    expect(start).toBeDisabled();

    await user.selectOptions(within(panel).getByLabelText('Вільна планета'), '70');
    await user.selectOptions(within(panel).getByLabelText('Корабель і пристрій'), '5:34');
    await user.click(within(panel).getByRole('button', { name: 'Запустити пристрій' }));

    // Nothing is sent before the confirmation, and the player can step back.
    expect(processes.terraform).not.toHaveBeenCalled();
    expect(
      within(panel).getByText(/Пристрій тераформінгу Mk I з трюму корабля №5 буде витрачено біля планети Kepler-4 безповоротно/),
    ).toBeInTheDocument();
    await user.click(within(panel).getByRole('button', { name: 'Не запускати' }));
    expect(processes.terraform).not.toHaveBeenCalled();

    await user.click(within(panel).getByRole('button', { name: 'Запустити пристрій' }));
    await user.click(within(panel).getByRole('button', { name: 'Підтвердити запуск' }));

    await waitFor(() => expect(processes.terraform).toHaveBeenCalledWith(70, 5, 34));
    expect(
      await within(panel).findByText('Планету Kepler-4 перетворено: збагачено родовищ 2, множники від 1,5 до 1,8.'),
    ).toBeInTheDocument();
  });

  it('keeps the button inactive without a ship with a device or a free body, as a hint only', async () => {
    const user = userEvent.setup();
    const processes = fakeProcessesApi({
      terraforming: vi.fn().mockResolvedValue(sampleTerraforming({ targets: [sampleTerraformingTarget({ shipIds: [] })] })),
    });
    renderPage('/planet', { auth: signedIn(), processes });

    const panel = await screen.findByRole('region', { name: 'Тераформінг' });
    await user.selectOptions(await within(panel).findByLabelText('Вільна планета'), '70');
    const start = within(panel).getByRole('button', { name: 'Запустити пристрій' });

    expect(start).toBeDisabled();
    expect(start).toHaveAccessibleDescription('Біля планети немає корабля з пристроєм тераформінгу в трюмі');
  });

  it('keeps the button inactive when no ship stands by a free body', async () => {
    const processes = fakeProcessesApi({
      terraforming: vi.fn().mockResolvedValue(sampleTerraforming({ targets: [] })),
    });
    renderPage('/planet', { auth: signedIn(), processes });

    const panel = await screen.findByRole('region', { name: 'Тераформінг' });
    const start = await within(panel).findByRole('button', { name: 'Запустити пристрій' });

    expect(start).toBeDisabled();
    expect(start).toHaveAccessibleDescription('Жоден ваш корабель не стоїть біля вільної заселюваної планети');
  });

  it('shows the reason of the server when the start is refused', async () => {
    const user = userEvent.setup();
    const processes = fakeProcessesApi({
      terraform: vi.fn().mockRejectedValue(new ApiError(422, 'processes_target_not_colonizable')),
    });
    renderPage('/planet', { auth: signedIn(), processes });

    const panel = await screen.findByRole('region', { name: 'Тераформінг' });
    await user.selectOptions(await within(panel).findByLabelText('Вільна планета'), '70');
    await user.selectOptions(within(panel).getByLabelText('Корабель і пристрій'), '5:34');
    await user.click(within(panel).getByRole('button', { name: 'Запустити пристрій' }));
    await user.click(within(panel).getByRole('button', { name: 'Підтвердити запуск' }));

    expect(
      await within(panel).findByText('Планету не можна заселити (наприклад, газовий гігант): тераформувати можна лише заселювану планету'),
    ).toBeInTheDocument();
    expect(processes.terraforming).toHaveBeenCalledTimes(2);
  });
});
