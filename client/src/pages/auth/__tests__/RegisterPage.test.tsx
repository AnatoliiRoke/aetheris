import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/http';
import { fakeAuthApi, renderPage } from '../../../test/renderPage';

async function fill(values: { email?: string; nickname?: string; password?: string; repeat?: string; terms?: boolean }) {
  const user = userEvent.setup();

  if (values.email) {
    await user.type(screen.getByLabelText('Електронна пошта'), values.email);
  }

  if (values.nickname) {
    await user.type(screen.getByLabelText('Нік'), values.nickname);
  }

  if (values.password) {
    await user.type(screen.getByLabelText('Пароль'), values.password);
  }

  if (values.repeat) {
    await user.type(screen.getByLabelText('Повтори пароль'), values.repeat);
  }

  if (values.terms) {
    await user.click(screen.getByRole('checkbox'));
  }

  await user.click(screen.getByRole('button', { name: 'Створити акаунт' }));
}

describe('RegisterPage', () => {
  it('checks the password rules, the nickname, the repetition and the terms before sending', async () => {
    const auth = fakeAuthApi();
    renderPage('/register', { auth });

    await fill({ email: 'pilot@example.test', nickname: 'P!', password: 'short', repeat: 'other' });

    expect(screen.getByText(/Пароль закороткий: потрібно щонайменше 8 символів\./)).toBeInTheDocument();
    expect(screen.getByText(/Додай у пароль хоча б одну цифру\./)).toBeInTheDocument();
    expect(screen.getByText(/Додай у пароль хоча б одну велику літеру\./)).toBeInTheDocument();
    expect(screen.getByText('Нік закороткий: потрібно щонайменше 3 символи.')).toBeInTheDocument();
    expect(screen.getByText('Паролі не збігаються.')).toBeInTheDocument();
    expect(screen.getByText('Щоб продовжити, прийми умови.')).toBeInTheDocument();
    expect(auth.register).not.toHaveBeenCalled();
  });

  it('registers and continues to the check-email screen', async () => {
    const auth = fakeAuthApi();
    renderPage('/register', { auth });

    await fill({
      email: 'pilot@example.test',
      nickname: 'Pilot One',
      password: 'Violet-harbor-lantern-42',
      repeat: 'Violet-harbor-lantern-42',
      terms: true,
    });

    await waitFor(() => expect(window.location.pathname).toBe('/check-email'));
    expect(auth.register).toHaveBeenCalledWith({
      email: 'pilot@example.test',
      nickname: 'Pilot One',
      password: 'Violet-harbor-lantern-42',
      passwordConfirmation: 'Violet-harbor-lantern-42',
    });
    expect(screen.getByText('pilot@example.test')).toBeInTheDocument();
  });

  it('shows the rules the server refused next to their fields', async () => {
    const auth = fakeAuthApi({
      register: vi.fn().mockRejectedValue(
        new ApiError(400, 'validation_failed', { Password: ['password_too_common'], Nickname: ['nickname_taken'] }),
      ),
    });
    renderPage('/register', { auth });

    await fill({
      email: 'pilot@example.test',
      nickname: 'Pilot',
      password: 'Password123456',
      repeat: 'Password123456',
      terms: true,
    });

    expect(await screen.findByText('Цей пароль надто поширений. Обери інший.')).toBeInTheDocument();
    expect(screen.getByText('Цей нік уже зайнятий.')).toBeInTheDocument();
    expect(screen.getByLabelText('Пароль')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByLabelText('Нік')).toHaveAttribute('aria-invalid', 'true');
  });
});
