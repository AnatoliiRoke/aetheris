import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { fakeAuthApi, renderPage } from '../../../test/renderPage';

describe('LandingPage', () => {
  it('shows every block of the design', async () => {
    renderPage('/');

    expect(screen.getByRole('heading', { level: 1, name: 'Збудуй свою імперію серед зірок' })).toBeInTheDocument();

    for (const title of [
      'Чотири речі, з яких складається твоя імперія',
      'Обери, під чиїм прапором летіти',
      'Як це виглядає в грі',
      'Топ-10 командирів',
      'Що нового у всесвіті',
      'Часті питання',
    ]) {
      expect(screen.getByRole('heading', { level: 2, name: title })).toBeInTheDocument();
    }

    expect(screen.getByRole('contentinfo')).toBeInTheDocument();
  });

  it('renders the top 10 from the content interface', async () => {
    renderPage('/');

    const table = await screen.findByRole('table');
    const rows = within(table).getAllByRole('row');

    // The header row and ten players.
    expect(rows).toHaveLength(11);
    expect(within(rows[1]).getByText('Kapitan_Zoria')).toBeInTheDocument();
    expect(within(rows[1]).getByText('Терани')).toBeInTheDocument();
  });

  it('reports a ranking that could not be loaded', async () => {
    renderPage('/', {
      content: {
        getTopPlayers: vi.fn().mockRejectedValue(new Error('offline')),
        getLatestNews: vi.fn().mockResolvedValue([]),
      },
    });

    expect(await screen.findByText('Не вдалося завантажити рейтинг. Спробуй пізніше.')).toBeInTheDocument();
  });

  it('switches the language of every text and remembers it', async () => {
    const user = userEvent.setup();
    renderPage('/');

    await user.click(screen.getAllByRole('button', { name: 'EN' })[0]);

    expect(screen.getByRole('heading', { level: 1, name: 'Build your empire among the stars' })).toBeInTheDocument();
    expect(await screen.findByText('Update 0.9: shield modules')).toBeInTheDocument();
    expect(document.documentElement.lang).toBe('en');
    expect(window.localStorage.getItem('aetheris.lang')).toBe('en');
  });

  it('keeps one FAQ answer open at a time', async () => {
    const user = userEvent.setup();
    renderPage('/');

    const first = screen.getByRole('button', { name: 'Гра справді безкоштовна?' });
    const second = screen.getByRole('button', { name: 'Що потрібно, щоб почати?' });

    expect(first).toHaveAttribute('aria-expanded', 'true');

    await user.click(second);

    expect(first).toHaveAttribute('aria-expanded', 'false');
    expect(second).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText('Лише браузер і акаунт. Нічого завантажувати чи встановлювати не треба.')).toBeVisible();
  });

  it('switches the screenshot with the thumbnails and the arrow keys', async () => {
    const user = userEvent.setup();
    renderPage('/');

    await user.click(screen.getByRole('button', { name: /Карта всесвіту/ }));
    expect(screen.getByText('Кадр 2 з 4 — Карта всесвіту')).toBeInTheDocument();

    await user.keyboard('{ArrowRight}');
    expect(screen.getByText('Кадр 3 з 4 — Конструктор кораблів')).toBeInTheDocument();
  });

  it('opens and closes the mobile menu', async () => {
    const user = userEvent.setup();
    renderPage('/');

    const toggle = screen.getByRole('button', { name: 'Відкрити меню' });
    await user.click(toggle);

    expect(toggle).toHaveAttribute('aria-expanded', 'true');
    expect(document.getElementById('mobile-menu')).toBeVisible();

    await user.keyboard('{Escape}');
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
  });

  it('offers sign-out to a signed-in player', async () => {
    const user = userEvent.setup();
    const auth = fakeAuthApi({ me: vi.fn().mockResolvedValue({ userId: 'user-1' }) });
    renderPage('/', { auth });

    const signOut = (await screen.findAllByRole('button', { name: 'Вийти' }))[0];
    await user.click(signOut);

    expect(auth.logout).toHaveBeenCalled();
    expect((await screen.findAllByRole('link', { name: 'Увійти' })).length).toBeGreaterThan(0);
  });
});
