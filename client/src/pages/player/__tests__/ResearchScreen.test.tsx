import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/http';
import type { ResearchTree, Technology } from '../../../api/researchApi';
import { LANGUAGE_STORAGE_KEY } from '../../../i18n/I18nProvider';
import { fakeAuthApi, fakeResearchApi, fakeTextsApi, renderPage, sampleResearchTree, signedInMe } from '../../../test/renderPage';

const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

const technology = (overrides: Partial<Technology>): Technology => ({
  ...sampleResearchTree().technologies[0],
  ...overrides,
});

/**
 * A tree with two branches: the economy has three technologies in three states, the fleet none.
 */
const tree = (overrides: Partial<ResearchTree> = {}): ResearchTree =>
  sampleResearchTree({
    branches: [
      { branch: 'economy', nameKey: 'research.branch.economy.name' },
      { branch: 'fleet', nameKey: 'research.branch.fleet.name' },
    ],
    technologies: [
      technology({ id: 1, nameKey: 'research.technology.tech_1.name', status: 'available' }),
      technology({ id: 2, code: 'tech_2', nameKey: 'research.technology.tech_2.name', descriptionKey: 'research.technology.tech_2.description', status: 'learned', learnedLevel: 10, sortOrder: 2 }),
      technology({
        id: 3,
        code: 'tech_3',
        nameKey: 'research.technology.tech_3.name',
        descriptionKey: 'research.technology.tech_3.description',
        status: 'unavailable',
        reasonCode: 'research_center_level_unmet',
        researchCenterLevel: 5,
        sortOrder: 3,
      }),
    ],
    ...overrides,
  });

const texts = {
  texts: fakeTextsApi({
    uk: {
      'world.resource.resource_1.name': 'Залізо',
      'research.branch.economy.name': 'Економіка',
      'research.branch.fleet.name': 'Флот',
      'research.technology.tech_1.name': 'Глибинний видобуток',
      'research.technology.tech_1.description': 'Шахти видобувають більше металів.',
      'research.technology.tech_2.name': 'Логістика складів',
      'research.technology.tech_2.description': 'Склади вміщують більше.',
      'research.technology.tech_3.name': 'Плазмова енергетика',
      'research.technology.tech_3.description': 'Нові реактори.',
    },
    en: { 'research.branch.economy.name': 'Economy', 'research.technology.tech_1.name': 'Deep extraction' },
  }),
};

describe('ResearchPage: the screen of the research (task 00020)', () => {
  it('shows the queue, the tabs of the branches and the tiles of the technologies', async () => {
    renderPage('/planet/research', { auth: signedIn(), research: fakeResearchApi({ tree: vi.fn().mockResolvedValue(tree()) }), ...texts });

    expect(await screen.findByRole('heading', { level: 1, name: 'Дослідження' })).toBeInTheDocument();
    expect(await screen.findByRole('region', { name: 'Черга досліджень' })).toBeInTheDocument();
    const tabs = await screen.findByRole('tablist', { name: 'Гілки технологій' });

    expect(within(tabs).getByRole('tab', { name: 'Економіка', selected: true })).toBeInTheDocument();
    expect(within(tabs).getByRole('tab', { name: 'Флот', selected: false })).toBeInTheDocument();

    const tile = (await screen.findByRole('heading', { name: 'Глибинний видобуток' })).closest('li')!;
    expect(within(tile).getByText('Шахти видобувають більше металів.')).toBeInTheDocument();
    expect(within(tile).getByText('Видобуток металів ×1,05')).toBeInTheDocument();
    expect(within(tile).getByText('Залізо 200')).toBeInTheDocument();
    expect(within(tile).getByText('0:05:00')).toBeInTheDocument();
    expect(within(tile).getByRole('button', { name: 'Дослідити' })).toBeEnabled();
  });

  it('keeps a closed technology inactive and says why', async () => {
    renderPage('/planet/research', { auth: signedIn(), research: fakeResearchApi({ tree: vi.fn().mockResolvedValue(tree()) }), ...texts });

    const tile = (await screen.findByRole('heading', { name: 'Плазмова енергетика' })).closest('li')!;

    expect(within(tile).getByRole('button', { name: 'Дослідити' })).toBeDisabled();
    expect(within(tile).getByText('Потрібен науковий центр рівня 5.')).toBeInTheDocument();
  });

  it('starts a research from the chosen body', async () => {
    const user = userEvent.setup();
    const research = fakeResearchApi({ tree: vi.fn().mockResolvedValue(tree()) });
    renderPage('/planet/research', { auth: signedIn(), research, ...texts });

    const tile = (await screen.findByRole('heading', { name: 'Глибинний видобуток' })).closest('li')!;
    await user.click(within(tile).getByRole('button', { name: 'Дослідити' }));

    expect(research.enqueue).toHaveBeenCalledWith(10, 1, 1);
  });

  it('says why the server refused a research', async () => {
    const user = userEvent.setup();
    const research = fakeResearchApi({ tree: vi.fn().mockResolvedValue(tree()), enqueue: vi.fn().mockRejectedValue(new ApiError(422, 'research_queue_full')) });
    renderPage('/planet/research', { auth: signedIn(), research, ...texts });

    const tile = (await screen.findByRole('heading', { name: 'Глибинний видобуток' })).closest('li')!;
    await user.click(within(tile).getByRole('button', { name: 'Дослідити' }));

    expect(await screen.findByText('Черга досліджень заповнена')).toBeInTheDocument();
  });

  it('searches the technologies of the branch by name and by description', async () => {
    const user = userEvent.setup();
    renderPage('/planet/research', { auth: signedIn(), research: fakeResearchApi({ tree: vi.fn().mockResolvedValue(tree()) }), ...texts });

    await screen.findByRole('heading', { name: 'Глибинний видобуток' });
    const search = screen.getByRole('searchbox', { name: 'Пошук технології' });

    await user.type(search, 'логіст');
    expect(screen.getByRole('heading', { name: 'Логістика складів' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Глибинний видобуток' })).not.toBeInTheDocument();

    // The description is searched too.
    await user.clear(search);
    await user.type(search, 'реактори');
    expect(screen.getByRole('heading', { name: 'Плазмова енергетика' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Логістика складів' })).not.toBeInTheDocument();

    await user.clear(search);
    await user.type(search, 'такого немає');
    expect(screen.getByText('Нічого не знайдено. Змініть пошук або фільтр.')).toBeInTheDocument();
  });

  it('filters the technologies by state', async () => {
    const user = userEvent.setup();
    renderPage('/planet/research', { auth: signedIn(), research: fakeResearchApi({ tree: vi.fn().mockResolvedValue(tree()) }), ...texts });

    await screen.findByRole('heading', { name: 'Глибинний видобуток' });
    const filter = screen.getByRole('combobox', { name: 'Стан' });

    await user.selectOptions(filter, 'learned');
    expect(screen.getByRole('heading', { name: 'Логістика складів' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Глибинний видобуток' })).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Плазмова енергетика' })).not.toBeInTheDocument();

    await user.selectOptions(filter, 'all');
    expect(screen.getAllByRole('heading', { level: 3 })).toHaveLength(3);
  });

  it('shows an empty branch with the reason, and moves between the branches with the arrow keys', async () => {
    const user = userEvent.setup();
    renderPage('/planet/research', { auth: signedIn(), research: fakeResearchApi({ tree: vi.fn().mockResolvedValue(tree()) }), ...texts });

    await user.click(await screen.findByRole('tab', { name: 'Флот' }));
    expect(screen.getByText('Ця гілка поки порожня: технології з’являться згодом.')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Глибинний видобуток' })).not.toBeInTheDocument();

    screen.getByRole('tab', { name: 'Флот' }).focus();
    await user.keyboard('{ArrowLeft}');
    expect(await screen.findByRole('heading', { name: 'Глибинний видобуток' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Економіка' })).toHaveAttribute('aria-selected', 'true');
  });

  it('shows the loading state while the tree is not here yet', async () => {
    renderPage('/planet/research', { auth: signedIn(), research: fakeResearchApi({ tree: vi.fn().mockReturnValue(new Promise(() => undefined)) }) });

    expect(await screen.findByRole('status')).toHaveTextContent('Завантаження');
    expect(screen.queryByRole('tablist', { name: 'Гілки технологій' })).not.toBeInTheDocument();
  });

  it('shows the error with a retry when the tree did not load', async () => {
    const user = userEvent.setup();
    let down = true;
    const research = fakeResearchApi({ tree: vi.fn().mockImplementation(() => (down ? Promise.reject(new Error('down')) : Promise.resolve(tree()))) });
    renderPage('/planet/research', { auth: signedIn(), research, ...texts });

    expect(await screen.findByText('Не вдалося завантажити дослідження.')).toBeInTheDocument();
    down = false;
    await user.click(screen.getByRole('button', { name: 'Спробувати ще раз' }));

    expect(await screen.findByRole('tablist', { name: 'Гілки технологій' })).toBeInTheDocument();
  });

  it('says the tree is empty when the server sends no branch', async () => {
    renderPage('/planet/research', { auth: signedIn(), research: fakeResearchApi({ tree: vi.fn().mockResolvedValue(tree({ branches: [], technologies: [] })) }) });

    expect(await screen.findByText('Дерево технологій ще порожнє.')).toBeInTheDocument();
  });

  it('draws only the common frame and marks the research as the current section', async () => {
    const { container } = renderPage('/planet/research', { auth: signedIn() });

    await screen.findByRole('region', { name: 'Черга досліджень' });

    expect(container.querySelectorAll('header')).toHaveLength(1);
    expect(container.querySelectorAll('.civ-side')).toHaveLength(1);
    await waitFor(() => expect(within(container.querySelector<HTMLElement>('.civ-side')!).getByRole('link', { current: 'page' })).toHaveTextContent('Дослідження'));
  });

  it('speaks English', async () => {
    window.localStorage.setItem(LANGUAGE_STORAGE_KEY, 'en');
    renderPage('/planet/research', { auth: signedIn(), research: fakeResearchApi({ tree: vi.fn().mockResolvedValue(tree()) }), ...texts });

    expect(await screen.findByRole('heading', { level: 1, name: 'Research' })).toBeInTheDocument();
    expect(await screen.findByRole('region', { name: 'Research queue' })).toBeInTheDocument();
    expect(await screen.findByRole('searchbox', { name: 'Search for a technology' })).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'State' })).toBeInTheDocument();
  });
});
