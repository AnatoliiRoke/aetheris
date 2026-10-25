import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/http';
import { fakeAuthApi, renderPage } from '../../../test/renderPage';

async function fillAndSubmit(email: string, password: string) {
  const user = userEvent.setup();

  if (email) {
    await user.type(screen.getByLabelText('Електронна пошта'), email);
  }

  if (password) {
    await user.type(screen.getByLabelText('Пароль'), password);
  }

  await user.click(screen.getByRole('button', { name: 'Увійти' }));
}

describe('LoginPage', () => {
  it('checks the fields before calling the server', async () => {
    const auth = fakeAuthApi();
    renderPage('/login', { auth });

    await fillAndSubmit('not-an-address', '');

    expect(screen.getByText('Перевір адресу. Приклад: name@mail.com')).toBeInTheDocument();
    expect(screen.getByText('Введи пароль.')).toBeInTheDocument();
    expect(screen.getByLabelText('Електронна пошта')).toHaveAttribute('aria-invalid', 'true');
    expect(auth.login).not.toHaveBeenCalled();
  });

  it('signs in and goes to the home page', async () => {
    const auth = fakeAuthApi();
    renderPage('/login', { auth });

    await fillAndSubmit('pilot@example.test', 'violet-harbor-lantern-42');

    await waitFor(() => expect(window.location.pathname).toBe('/'));
    expect(auth.login).toHaveBeenCalledWith('pilot@example.test', 'violet-harbor-lantern-42');
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

  it('offers a new confirmation email for an unconfirmed address', async () => {
    const auth = fakeAuthApi({ login: vi.fn().mockRejectedValue(new ApiError(403, 'email_not_confirmed')) });
    renderPage('/login', { auth });

    await fillAndSubmit('pilot@example.test', 'any-password');

    const link = await screen.findByRole('link', { name: 'Надіслати лист ще раз' });
    expect(link).toHaveAttribute('href', '/check-email?email=pilot%40example.test');
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
