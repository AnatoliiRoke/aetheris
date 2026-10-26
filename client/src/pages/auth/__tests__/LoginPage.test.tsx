import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/http';
import { fakeAuthApi, renderPage, signedInMe } from '../../../test/renderPage';

async function fillAndSubmit(login: string, password: string, rememberMe = false) {
  const user = userEvent.setup();

  if (login) {
    await user.type(screen.getByLabelText('Пошта або нік'), login);
  }

  if (password) {
    await user.type(screen.getByLabelText('Пароль'), password);
  }

  if (rememberMe) {
    await user.click(screen.getByRole('checkbox', { name: 'Запам’ятати мене' }));
  }

  await user.click(screen.getByRole('button', { name: 'Увійти' }));
}

describe('LoginPage', () => {
  it('checks the fields before calling the server', async () => {
    const auth = fakeAuthApi();
    renderPage('/login', { auth });

    await fillAndSubmit('', '');

    expect(screen.getByText('Введи пошту або нік.')).toBeInTheDocument();
    expect(screen.getByText('Введи пароль.')).toBeInTheDocument();
    expect(screen.getByLabelText('Пошта або нік')).toHaveAttribute('aria-invalid', 'true');
    expect(auth.login).not.toHaveBeenCalled();
  });

  it('signs in with a nickname and "remember me" and goes to the game', async () => {
    const auth = fakeAuthApi({ me: vi.fn().mockResolvedValueOnce(null).mockResolvedValue(signedInMe()) });
    renderPage('/login', { auth });

    await fillAndSubmit('Pilot', 'Violet-harbor-lantern-42', true);

    await waitFor(() => expect(window.location.pathname).toBe('/game'));
    expect(auth.login).toHaveBeenCalledWith('Pilot', 'Violet-harbor-lantern-42', true);
    expect(await screen.findByRole('heading', { name: 'Вітаємо, Pilot' })).toBeInTheDocument();
  });

  it('opens the account page when the sign-in cancelled a deletion', async () => {
    const auth = fakeAuthApi({
      login: vi.fn().mockResolvedValue({ deletionCancelled: true }),
      me: vi.fn().mockResolvedValueOnce(null).mockResolvedValue(signedInMe()),
    });
    renderPage('/login', { auth });

    await fillAndSubmit('pilot@example.test', 'Violet-harbor-lantern-42');

    expect(await screen.findByText('Видалення скасовано')).toBeInTheDocument();
  });

  it('shows wrong credentials on the alert and on the password field', async () => {
    const auth = fakeAuthApi({ login: vi.fn().mockRejectedValue(new ApiError(401, 'invalid_credentials')) });
    renderPage('/login', { auth });

    await fillAndSubmit('pilot@example.test', 'wrong-password');

    expect(await screen.findByRole('alert')).toHaveTextContent('Не вдалося увійти');
    expect(screen.getByText('Невірний пароль. Спробуй ще раз або віднови його.')).toBeInTheDocument();
    expect(screen.getByLabelText('Пароль')).toHaveFocus();
  });

  it('explains a temporary lockout', async () => {
    const auth = fakeAuthApi({ login: vi.fn().mockRejectedValue(new ApiError(403, 'account_locked')) });
    renderPage('/login', { auth });

    await fillAndSubmit('pilot@example.test', 'any-password');

    expect(await screen.findByRole('alert')).toHaveTextContent('Акаунт тимчасово заблоковано');
  });

  it('keeps the Google button disabled when the server has no Google credentials', async () => {
    renderPage('/login', { auth: fakeAuthApi({ providers: vi.fn().mockResolvedValue({ google: false }) }) });

    expect(screen.getByRole('button', { name: /Google/ })).toBeDisabled();
    expect(screen.getByText('Вхід через Google поки вимкнено на сервері.')).toBeInTheDocument();
  });

  it('enables the Google button when the server has Google credentials', async () => {
    renderPage('/login', { auth: fakeAuthApi({ providers: vi.fn().mockResolvedValue({ google: true }) }) });

    await waitFor(() => expect(screen.getByRole('button', { name: /Google/ })).toBeEnabled());
  });
});
