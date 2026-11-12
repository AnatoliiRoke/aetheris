import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/http';
import {
  fakeAuthApi,
  fakeMailApi,
  renderPage,
  sampleMailDraft,
  sampleMailLetter,
  sampleMailLetterDetail,
  sampleMailView,
  signedInMe,
} from '../../../test/renderPage';

/**
 * A signed-in player with a civilization and a confirmed address.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

/**
 * The mail of a player with a letter of another player, a letter of the game and a report, so the
 * list shows the marks and the labels of the kinds of the letters (task 00060).
 */
const richView = () =>
  sampleMailView({
    folders: [
      { code: 'incoming', unreadCount: 2, totalCount: 3 },
      { code: 'sent', unreadCount: 0, totalCount: 1 },
      { code: 'saved', unreadCount: 0, totalCount: 1 },
      { code: 'trash', unreadCount: 0, totalCount: 0 },
    ],
    categories: [
      { id: 1, code: 'mail_category_1', nameKey: 'mail.category.players', imageKey: 'player', count: 1 },
      { id: 2, code: 'mail_category_2', nameKey: 'mail.category.system', imageKey: 'system', count: 1 },
      { id: 3, code: 'mail_category_3', nameKey: 'mail.category.reports', imageKey: 'report', count: 1 },
    ],
    letters: [
      sampleMailLetter({
        id: 1,
        kindCode: 'player',
        senderNickname: 'Krell_77',
        subject: 'Щодо Ісару',
        preview: 'Пропоную ненапад на 3 доби.',
        isRead: false,
        createdAt: '2026-11-12T08:15:00Z',
      }),
      sampleMailLetter({
        id: 2,
        kindCode: 'system',
        categoryId: 2,
        categoryNameKey: 'mail.category.system',
        senderPlayerId: null,
        senderNickname: null,
        senderRaceColorKey: null,
        subject: 'Склад металів заповнено',
        preview: 'Видобуток заліза й іридію зупинено.',
        isRead: true,
        createdAt: '2026-11-12T07:31:00Z',
      }),
      sampleMailLetter({
        id: 3,
        kindCode: 'report',
        categoryId: 3,
        categoryNameKey: 'mail.category.reports',
        systemKindCode: 'fleet_report',
        senderPlayerId: null,
        senderNickname: null,
        senderRaceColorKey: null,
        subject: '',
        preview: 'Флот «Вега» добув 400 заліза.',
        isRead: false,
        groupCount: 3,
        createdAt: '2026-11-11T12:00:00Z',
      }),
    ],
    unreadCount: 2,
    totalCount: 3,
    // The game time is midday, so the day of a letter does not depend on the hour of the machine
    // the test runs on.
    gameNow: '2026-11-12T12:00:00Z',
  });

describe('the screen of the mail (task 00060)', () => {
  it('shows the folders, the categories and the letters with their marks and their labels', async () => {
    renderPage('/mail', { auth: signedIn(), mail: fakeMailApi({ mail: vi.fn().mockResolvedValue(richView()) }) });

    // The head of the screen: the title, the number of the unread letters and the two buttons.
    expect(await screen.findByRole('heading', { name: 'Пошта' })).toBeInTheDocument();
    expect(screen.getByText('2 непрочитаних')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Прочитати все/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Новий лист/ })).toBeInTheDocument();

    // The folders with their counters. No folder is chosen yet: the list shows every letter.
    const folders = screen.getByRole('group', { name: 'Папки' });

    expect(within(folders).getByRole('button', { name: /Вхідні/ })).toHaveAttribute('aria-pressed', 'false');
    expect(within(folders).getByRole('button', { name: /Вхідні/ })).toHaveTextContent('2');
    expect(within(folders).getByRole('button', { name: /Надіслані/ })).toHaveAttribute('aria-pressed', 'false');

    // The categories come from the server as data: the client holds no table of their names.
    const categories = screen.getByRole('group', { name: 'Типи листів' });

    expect(within(categories).getByRole('button', { name: /Усі/ })).toHaveTextContent('3');
    expect(within(categories).getByRole('button', { name: /Гравці/ })).toHaveTextContent('1');
    expect(within(categories).getByRole('button', { name: /Система/ })).toHaveTextContent('1');
    expect(within(categories).getByRole('button', { name: /Звіти/ })).toHaveTextContent('1');

    // The list: the sender, the subject, the first words and the label of the kind.
    const list = screen.getByRole('region', { name: 'Список листів' });

    expect(within(list).getByText('Krell_77')).toBeInTheDocument();
    expect(within(list).getByText('Щодо Ісару')).toBeInTheDocument();
    expect(within(list).getByText('Пропоную ненапад на 3 доби.')).toBeInTheDocument();
    expect(within(list).getAllByText('Гравці').length).toBeGreaterThan(0);
    expect(within(list).getAllByText('Система').length).toBeGreaterThan(0);
    // The letters of the game that stand for several ones are counted in one row.
    expect(within(list).getByText('3 лист.')).toBeInTheDocument();

    // The groups of the days.
    expect(within(list).getByText('Сьогодні')).toBeInTheDocument();
    expect(within(list).getByText('Вчора')).toBeInTheDocument();
  });

  it('shows the empty states of the list and of the panel', async () => {
    renderPage('/mail', { auth: signedIn(), mail: fakeMailApi() });

    expect(await screen.findByText('Тут поки немає листів.')).toBeInTheDocument();
    expect(screen.getByText('Оберіть лист.')).toBeInTheDocument();
  });

  it('shows the text of a letter, its actions and the quick reply when the player opens it', async () => {
    const user = userEvent.setup();
    const detail = sampleMailLetterDetail({
      letter: sampleMailLetter({ id: 1, senderNickname: 'Krell_77', subject: 'Щодо Ісару' }),
      body: 'Командоре, вітаю.\n\nПропоную ненапад на 3 доби.',
    });

    renderPage('/mail', {
      auth: signedIn(),
      mail: fakeMailApi({
        mail: vi.fn().mockResolvedValue(richView()),
        letter: vi.fn().mockResolvedValue(detail),
      }),
    });

    await user.click(await screen.findByRole('button', { name: /Щодо Ісару/ }));

    const panel = await screen.findByRole('region', { name: 'Лист' });

    expect(within(panel).getByRole('heading', { name: 'Щодо Ісару' })).toBeInTheDocument();
    expect(within(panel).getByText('Командоре, вітаю.')).toBeInTheDocument();
    expect(within(panel).getByText('Пропоную ненапад на 3 доби.')).toBeInTheDocument();
    expect(within(panel).getByText('→ Вам')).toBeInTheDocument();

    // The actions of the letter.
    expect(within(panel).getByRole('button', { name: /Відповісти/ })).toBeInTheDocument();
    expect(within(panel).getByRole('button', { name: /Переслати/ })).toBeInTheDocument();
    expect(within(panel).getByRole('button', { name: /Зберегти/ })).toBeInTheDocument();
    expect(within(panel).getByRole('button', { name: /Видалити/ })).toBeInTheDocument();
    expect(within(panel).getByRole('button', { name: /Поскаржитись/ })).toBeInTheDocument();

    // The quick reply with the counter of the text.
    expect(within(panel).getByText('0 / 2000')).toBeInTheDocument();
  });

  it('sends a letter of the player and reads the list again', async () => {
    const user = userEvent.setup();
    const sendLetter = vi.fn().mockResolvedValue({ id: 5 });
    const mail = vi.fn().mockResolvedValue(richView());

    renderPage('/mail', {
      auth: signedIn(),
      mail: fakeMailApi({ mail, sendLetter, recipients: vi.fn().mockResolvedValue({ players: [{ playerId: 'p2', nickname: 'Krell_77', raceColorKey: 'race-3', raceNameKey: 'world.race.race_3.name', hasWritten: false }] }) }),
    });

    await user.click(await screen.findByRole('button', { name: /Новий лист/ }));

    // The form of a new letter: the receiver, the subject, the text and the buttons.
    expect(await screen.findByRole('heading', { name: 'Новий лист' })).toBeInTheDocument();
    expect(screen.getByLabelText('Кому')).toBeInTheDocument();
    expect(screen.getByLabelText('Тема')).toBeInTheDocument();
    expect(screen.getByLabelText('Повідомлення')).toBeInTheDocument();

    // The hint of the players.
    expect(await screen.findByRole('option', { name: /Krell_77/ })).toBeInTheDocument();

    await user.type(screen.getByLabelText('Кому'), 'Krell_77');
    await user.type(screen.getByLabelText('Тема'), 'Обмін кристалами');
    await user.type(screen.getByLabelText('Повідомлення'), 'Привіт!');

    // The counter of the text follows the text. The number and the limit are two nodes, so the
    // counter is read as the text of the whole block.
    expect(document.querySelector('.mail-field__count')?.textContent).toBe('7 / 2000');

    await user.click(within(screen.getByRole('form')).getByRole('button', { name: /Надіслати/ }));

    await waitFor(() => expect(sendLetter).toHaveBeenCalledWith({ receiverNickname: 'Krell_77', subject: 'Обмін кристалами', body: 'Привіт!' }));
    // The list is read again after the command, so the screen shows the new state.
    await waitFor(() => expect(mail.mock.calls.length).toBeGreaterThan(1));
  });

  it('refuses a letter with a link and shows why', async () => {
    const user = userEvent.setup();
    const sendLetter = vi.fn().mockRejectedValue(new ApiError(400, 'mail_link_not_allowed'));

    renderPage('/mail', { auth: signedIn(), mail: fakeMailApi({ mail: vi.fn().mockResolvedValue(richView()), sendLetter }) });

    await user.click(await screen.findByRole('button', { name: /Новий лист/ }));

    await user.type(await screen.findByLabelText('Кому'), 'Krell_77');
    await user.type(screen.getByLabelText('Тема'), 'Тема');
    await user.type(screen.getByLabelText('Повідомлення'), 'Дивись https://example.com');
    await user.click(within(screen.getByRole('form')).getByRole('button', { name: /Надіслати/ }));

    // The refusal of the server is shown: the screen decides no rule of its own.
    expect(await screen.findByText(/має бути|посил/i)).toBeInTheDocument();
  });

  it('marks every letter of the folder as read and reads the list again', async () => {
    const user = userEvent.setup();
    const readAll = vi.fn().mockResolvedValue({ marked: 2 });
    const mail = vi.fn().mockResolvedValue(richView());

    renderPage('/mail', { auth: signedIn(), mail: fakeMailApi({ mail, readAll }) });

    await user.click(await screen.findByRole('button', { name: /Прочитати все/ }));

    await waitFor(() => expect(readAll).toHaveBeenCalledWith('incoming'));
    await waitFor(() => expect(mail.mock.calls.length).toBeGreaterThan(1));
  });

  it('empties the bin and reads the list again', async () => {
    const user = userEvent.setup();
    const emptyTrash = vi.fn().mockResolvedValue({ removed: 4 });
    const mail = vi.fn().mockResolvedValue(richView());

    renderPage('/mail', { auth: signedIn(), mail: fakeMailApi({ mail, emptyTrash }) });

    // The bin is a folder of the mail, so the button of the cleaning is there.
    await user.click(await screen.findByRole('button', { name: /Кошик/ }));

    await waitFor(() => expect(mail).toHaveBeenCalled());
    await user.click(await screen.findByRole('button', { name: /Очистити кошик/ }));

    await waitFor(() => expect(emptyTrash).toHaveBeenCalled());
  });

  it('opens a draft of the player in the form and removes it', async () => {
    const user = userEvent.setup();
    const removeDraft = vi.fn().mockResolvedValue({ removed: true });

    renderPage('/mail', {
      auth: signedIn(),
      mail: fakeMailApi({
        mail: vi.fn().mockResolvedValue(richView()),
        drafts: vi.fn().mockResolvedValue({ drafts: [sampleMailDraft({ id: 3, subject: 'Обмін кристалами', body: 'Привіт!' })] }),
        removeDraft,
      }),
    });

    await user.click(await screen.findByText('Обмін кристалами'));

    // The form starts from the draft.
    expect(await screen.findByLabelText('Тема')).toHaveValue('Обмін кристалами');
    expect(screen.getByLabelText('Повідомлення')).toHaveValue('Привіт!');

    await user.click(screen.getByRole('button', { name: /Видалити чернетку/ }));

    await waitFor(() => expect(removeDraft).toHaveBeenCalledWith(3));
  });

  it('shows the number of the unread letters in the top bar', async () => {
    renderPage('/mail', {
      auth: signedIn(),
      mail: fakeMailApi({
        mail: vi.fn().mockResolvedValue(richView()),
        unread: vi.fn().mockResolvedValue({ unreadCount: 2 }),
      }),
    });

    const header = await waitFor(() => {
      const found = document.querySelector<HTMLElement>('.civ-topbar');

      expect(found).not.toBeNull();

      return found!;
    });

    await waitFor(() => expect(header.querySelector('.civ-ibtn__count')).toHaveTextContent('2'));
  });

  it('shows why the mail could not be loaded', async () => {
    renderPage('/mail', { auth: signedIn(), mail: fakeMailApi({ mail: vi.fn().mockRejectedValue(new ApiError(500, 'internal_error')) }) });

    expect(await screen.findByText('Не вдалося завантажити пошту.')).toBeInTheDocument();
  });
});
