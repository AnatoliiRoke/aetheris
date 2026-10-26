import { act, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/http';
import { siteConfig } from '../../../config/site';
import { fakeAuthApi, renderPage } from '../../../test/renderPage';

describe('ForgotPasswordPage', () => {
  it('requests a reset link and shows the check-email screen for a reset', async () => {
    const user = userEvent.setup();
    const auth = fakeAuthApi();
    renderPage('/forgot-password', { auth });

    await user.type(screen.getByLabelText('Електронна пошта'), 'pilot@example.test');
    await user.click(screen.getByRole('button', { name: 'Надіслати посилання' }));

    await waitFor(() => expect(window.location.pathname).toBe('/check-email'));
    expect(auth.forgotPassword).toHaveBeenCalledWith('pilot@example.test');
    expect(screen.getByText(/ми надіслали на неї посилання для нового пароля/)).toBeInTheDocument();
  });
});

describe('CheckEmailPage', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('allows a new confirmation email only after the cooldown', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    const auth = fakeAuthApi();
    renderPage('/check-email?email=pilot%40example.test', { auth });

    const resend = screen.getByRole('button', { name: /Надіслати лист ще раз/ });
    expect(resend).toBeDisabled();

    for (let second = 0; second < siteConfig.resendCooldownSeconds; second++) {
      await act(async () => {
        await vi.advanceTimersByTimeAsync(1000);
      });
    }

    expect(resend).toBeEnabled();

    await act(async () => {
      resend.click();
    });

    expect(auth.resendConfirmation).toHaveBeenCalledWith('pilot@example.test');
    expect(await screen.findByText('✓ Лист надіслано ще раз.')).toBeInTheDocument();
  });
});

describe('ConfirmEmailPage', () => {
  it('confirms the address from the link', async () => {
    const auth = fakeAuthApi();
    renderPage('/confirm-email?userId=user-1&token=abc', { auth });

    expect(await screen.findByText('Пошту підтверджено')).toBeInTheDocument();
    expect(auth.confirmEmail).toHaveBeenCalledWith('user-1', 'abc');
  });

  it('explains an invalid link', async () => {
    const auth = fakeAuthApi({ confirmEmail: vi.fn().mockRejectedValue(new ApiError(400, 'invalid_token')) });
    renderPage('/confirm-email?userId=user-1&token=old', { auth });

    expect(await screen.findByRole('alert')).toHaveTextContent('Посилання недійсне');
  });
});

describe('ResetPasswordPage', () => {
  it('saves a new password', async () => {
    const user = userEvent.setup();
    const auth = fakeAuthApi();
    renderPage('/reset-password?userId=user-1&token=abc', { auth });

    await user.type(screen.getByLabelText('Пароль'), 'Amber-meadow-compass-58');
    await user.click(screen.getByRole('button', { name: 'Зберегти пароль' }));

    expect(await screen.findByText('Пароль змінено')).toBeInTheDocument();
    expect(auth.resetPassword).toHaveBeenCalledWith('user-1', 'abc', 'Amber-meadow-compass-58');
  });
});

describe('Unknown address', () => {
  it('shows the not-found page', () => {
    renderPage('/does-not-exist');

    expect(screen.getByRole('heading', { name: 'Такої сторінки немає' })).toBeInTheDocument();
  });
});
