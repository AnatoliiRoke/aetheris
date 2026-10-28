import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/http';
import { fakeAuthApi, fakeGameApi, fakeResearchApi, renderPage, signedInMe } from '../../../test/renderPage';

describe('Player gate', () => {
  it('sends a visitor to the sign-in', async () => {
    renderPage('/game');

    await waitFor(() => expect(window.location.pathname).toBe('/login'));
  });

  it('shows only the first step until the civilization exists', async () => {
    const auth = fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe({ civilization: null })) });
    renderPage('/account', { auth });

    await waitFor(() => expect(window.location.pathname).toBe('/civilization'));
    expect(await screen.findByRole('heading', { name: 'Створи свою цивілізацію' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Акаунт' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Вийти' })).toBeInTheDocument();
  });
});

describe('CivilizationPage', () => {
  it('needs a race and a valid planet name before sending', async () => {
    const user = userEvent.setup();
    const game = fakeGameApi();
    const auth = fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe({ civilization: null })) });
    renderPage('/civilization', { auth, game });

    await user.click(await screen.findByRole('button', { name: 'Створити цивілізацію' }));

    expect(screen.getByText('Обери расу.')).toBeInTheDocument();
    expect(screen.getByText('Назва закоротка: потрібно щонайменше 3 символи.')).toBeInTheDocument();
    expect(game.foundCivilization).not.toHaveBeenCalled();
  });

  it('creates the civilization and opens the game', async () => {
    const user = userEvent.setup();
    const game = fakeGameApi();
    const auth = fakeAuthApi({
      me: vi.fn().mockResolvedValueOnce(signedInMe({ civilization: null })).mockResolvedValue(signedInMe()),
    });
    renderPage('/civilization', { auth, game });

    await user.click(await screen.findByRole('radio', { name: /Кварри/ }));
    await user.type(screen.getByLabelText('Назва планети'), 'Нова Прима');
    await user.click(screen.getByRole('button', { name: 'Створити цивілізацію' }));

    await waitFor(() => expect(window.location.pathname).toBe('/game'));
    expect(game.foundCivilization).toHaveBeenCalledWith(2, 'Нова Прима');
  });

  it('shows the name rules the server refused', async () => {
    const user = userEvent.setup();
    const game = fakeGameApi({
      foundCivilization: vi.fn().mockRejectedValue(new ApiError(400, 'validation_failed', { planet_name: ['planet_name_restricted'] })),
    });
    const auth = fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe({ civilization: null })) });
    renderPage('/civilization', { auth, game });

    await user.click(await screen.findByRole('radio', { name: /Терани/ }));
    await user.type(screen.getByLabelText('Назва планети'), 'Погана назва');
    await user.click(screen.getByRole('button', { name: 'Створити цивілізацію' }));

    expect(await screen.findByText('Таку назву не можна використати. Обери іншу.')).toBeInTheDocument();
  });
});

describe('GamePage', () => {
  it('shows the banner and keeps the game sections closed before the confirmation', async () => {
    const user = userEvent.setup();
    const auth = fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe({ emailConfirmed: false })) });
    renderPage('/game', { auth });

    expect(await screen.findByText('Підтверди пошту')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Будівництво' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Ринок' })).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'Надіслати лист ще раз' }));

    expect(auth.resendMyConfirmation).toHaveBeenCalled();
    expect(await screen.findByText(/Лист надіслано\./)).toBeInTheDocument();
  });

  it('has no banner for a confirmed address', async () => {
    renderPage('/game', { auth: fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) }) });

    expect(await screen.findByRole('heading', { name: 'Вітаємо, Pilot' })).toBeInTheDocument();
    expect(screen.queryByText('Підтверди пошту')).not.toBeInTheDocument();
    expect(screen.getByText('Nova')).toBeInTheDocument();
  });

  it('shows the tree of the technologies with the real data', async () => {
    const research = fakeResearchApi();
    renderPage('/game', { auth: fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) }), research });

    const panel = await screen.findByRole('region', { name: 'Дослідження' });

    expect(await within(panel).findByText('Глибинний видобуток')).toBeInTheDocument();
    expect(within(panel).getByText('Рівень: 0 з 10')).toBeInTheDocument();
    expect(within(panel).getByText('Науковий центр: рівень 2.')).toBeInTheDocument();
    expect(research.tree).toHaveBeenCalledWith(10);
  });

  it('puts a technology in the queue and returns the whole cost', async () => {
    const user = userEvent.setup();
    const research = fakeResearchApi();
    renderPage('/game', { auth: fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) }), research });

    await user.click(await screen.findByRole('button', { name: 'Дослідити' }));

    await waitFor(() => expect(research.enqueue).toHaveBeenCalledWith(10, 1, 1));
  });

  it('keeps the research closed until the e-mail is confirmed', async () => {
    const auth = fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe({ emailConfirmed: false })) });
    renderPage('/game', { auth });

    expect(await screen.findByText('Підтверди пошту')).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Дослідження' })).not.toBeInTheDocument();
  });
});

describe('AccountPage', () => {
  it('shows the e-mail address without a way to change it', async () => {
    renderPage('/account', { auth: fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) }) });

    expect(await screen.findByText('pilot@example.test')).toBeInTheDocument();
    expect(screen.getByText(/Пошту змінює лише підтримка/)).toBeInTheDocument();
    expect(screen.queryByLabelText('Електронна пошта')).not.toBeInTheDocument();
  });

  it('changes the nickname and explains the pause between changes', async () => {
    const user = userEvent.setup();
    const auth = fakeAuthApi({
      me: vi.fn().mockResolvedValue(signedInMe()),
      changeNickname: vi
        .fn()
        .mockResolvedValueOnce({ nickname: 'Pilot Two', nextChangeAt: '2026-11-25T00:00:00Z' })
        .mockRejectedValueOnce(new ApiError(422, 'nickname_change_too_soon')),
    });
    renderPage('/account', { auth });

    const field = await screen.findByLabelText('Нік');
    await user.clear(field);
    await user.type(field, 'Pilot Two');
    await user.click(screen.getByRole('button', { name: 'Зберегти нік' }));

    expect(await screen.findByText(/Нік змінено/)).toBeInTheDocument();
    expect(auth.changeNickname).toHaveBeenCalledWith('Pilot Two');

    await user.clear(field);
    await user.type(field, 'Pilot Three');
    await user.click(screen.getByRole('button', { name: 'Зберегти нік' }));

    expect(await screen.findByText('Нік змінювали нещодавно')).toBeInTheDocument();
  });

  it('changes the password with the current one', async () => {
    const user = userEvent.setup();
    const auth = fakeAuthApi({
      me: vi.fn().mockResolvedValue(signedInMe()),
      changePassword: vi.fn().mockRejectedValueOnce(new ApiError(400, 'validation_failed', { current_password: ['wrong_password'] })),
    });
    renderPage('/account', { auth });

    await user.type(await screen.findByLabelText('Поточний пароль'), 'Old-password-1');
    await user.type(screen.getByLabelText('Новий пароль'), 'New-password-22');
    await user.type(screen.getByLabelText('Повтори пароль'), 'New-password-22');
    await user.click(screen.getByRole('button', { name: 'Зберегти пароль' }));

    expect(await screen.findByText('Невірний пароль.')).toBeInTheDocument();
    expect(auth.changePassword).toHaveBeenCalledWith('Old-password-1', 'New-password-22', 'New-password-22');
  });

  it('signs out of every device', async () => {
    const user = userEvent.setup();
    const auth = fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });
    renderPage('/account', { auth });

    await user.click(await screen.findByRole('button', { name: 'Вийти з усіх пристроїв' }));

    expect(auth.signOutEverywhere).toHaveBeenCalled();
    await waitFor(() => expect(window.location.pathname).toBe('/login'));
  });

  it('requests the deletion with the password and tells how to cancel it', async () => {
    const user = userEvent.setup();
    const auth = fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });
    renderPage('/account', { auth });

    await user.type(await screen.findByLabelText('Пароль'), 'Violet-harbor-lantern-42');
    await user.click(screen.getByRole('button', { name: 'Видалити акаунт' }));

    expect(auth.deleteAccount).toHaveBeenCalledWith('Violet-harbor-lantern-42');
    expect(await screen.findByText('Акаунт буде видалено')).toBeInTheDocument();
  });
});
