import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/http';
import { fakeAuthApi, renderPage } from '../../../test/renderPage';

describe('RegisterPage', () => {
  it('requires a long enough password and the accepted terms', async () => {
    const user = userEvent.setup();
    const auth = fakeAuthApi();
    renderPage('/register', { auth });

    await user.type(screen.getByLabelText('Електронна пошта'), 'pilot@example.test');
    await user.type(screen.getByLabelText('Пароль'), 'short');
    await user.click(screen.getByRole('button', { name: 'Створити акаунт' }));

    expect(screen.getByText('Пароль закороткий: потрібно щонайменше 12 символів.')).toBeInTheDocument();
    expect(screen.getByText('Щоб продовжити, прийми умови.')).toBeInTheDocument();
    expect(auth.register).not.toHaveBeenCalled();
  });

  it('registers and continues to the check-email screen', async () => {
    const user = userEvent.setup();
    const auth = fakeAuthApi();
    renderPage('/register', { auth });

    await user.type(screen.getByLabelText('Електронна пошта'), 'pilot@example.test');
    await user.type(screen.getByLabelText('Пароль'), 'violet-harbor-lantern-42');
    await user.click(screen.getByRole('checkbox'));
    await user.click(screen.getByRole('button', { name: 'Створити акаунт' }));

    await waitFor(() => expect(window.location.pathname).toBe('/check-email'));
    expect(auth.register).toHaveBeenCalledWith('pilot@example.test', 'violet-harbor-lantern-42');
    expect(screen.getByText('pilot@example.test')).toBeInTheDocument();
  });

  it('shows the password rules the server refused', async () => {
    const user = userEvent.setup();
    const auth = fakeAuthApi({
      register: vi
        .fn()
        .mockRejectedValue(new ApiError(400, 'validation_failed', { Password: ['password_too_common'] })),
    });
    renderPage('/register', { auth });

    await user.type(screen.getByLabelText('Електронна пошта'), 'pilot@example.test');
    await user.type(screen.getByLabelText('Пароль'), 'password123456');
    await user.click(screen.getByRole('checkbox'));
    await user.click(screen.getByRole('button', { name: 'Створити акаунт' }));

    expect(await screen.findByText('Цей пароль надто поширений. Обери інший.')).toBeInTheDocument();
    expect(screen.getByLabelText('Пароль')).toHaveAttribute('aria-invalid', 'true');
  });
});
