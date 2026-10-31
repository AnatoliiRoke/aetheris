import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { LANGUAGE_STORAGE_KEY } from '../../../i18n/I18nProvider';
import { fakeAuthApi, fakePlanetApi, fakeProductionApi, renderPage, samplePlanet, sampleProductionStock, signedInMe } from '../../../test/renderPage';

const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

const tabNames = ['Усе', 'Ресурси', 'Продукція', 'Модулі', 'Корпуси', 'Артефакти'];

describe('StoragePage: the screen of the storage (task 00020)', () => {
  it('has the six tabs and shows every block on the first one', async () => {
    renderPage('/planet/storage', { auth: signedIn() });

    const tabs = await screen.findByRole('tablist', { name: 'Розділи складу' });

    expect(within(tabs).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(tabNames);
    expect(within(tabs).getByRole('tab', { name: 'Усе', selected: true })).toBeInTheDocument();

    expect(await screen.findByRole('region', { name: 'Ресурси' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Продукція' })).toBeInTheDocument();
    expect(await screen.findByRole('region', { name: 'Модулі кораблів' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Корпуси кораблів' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Артефакти' })).toBeInTheDocument();
  });

  it('shows the real resources under their group with the amount against the capacity of the resource', async () => {
    renderPage('/planet/storage', { auth: signedIn() });

    const block = await screen.findByRole('region', { name: 'Ресурси' });
    const iron = within(block).getByText('Залізо').closest('tr')!;
    const copper = within(block).getByText('Мідь').closest('tr')!;

    // The columns of the design: the name, the amount, the rate per hour and the fill.
    expect(within(block).getAllByRole('columnheader').map((cell) => cell.textContent)).toEqual(['Назва', 'Кількість', 'За годину', 'Заповнення', 'Видобувні']);
    // Every resource has the capacity of its own, so 900 of 10 000 is 9 percent of the iron and
    // 500 of 10 000 is 5 percent of the copper.
    expect(within(iron).getByText('900 / 10 000')).toBeInTheDocument();
    expect(within(iron).getByRole('progressbar', { name: 'Залізо' })).toHaveAttribute('aria-valuenow', '9');
    expect(within(iron).getByText('9%')).toBeInTheDocument();
    expect(within(copper).getByText('500 / 10 000')).toBeInTheDocument();
    expect(within(copper).getByRole('progressbar', { name: 'Мідь' })).toHaveAttribute('aria-valuenow', '5');
    expect(within(copper).getByText('5%')).toBeInTheDocument();
  });

  it('marks only the full resource with the badge and names it in the alarm', async () => {
    const planets = fakePlanetApi({
      planet: vi.fn().mockResolvedValue(
        samplePlanet({
          resources: [
            { resourceTypeId: 1, resourceGroupId: 1, amount: 4200, ratePerHour: 0, capacity: 4200, isFull: true, fullAt: '2026-10-26T11:00:00Z' },
            { resourceTypeId: 3, resourceGroupId: 1, amount: 0, ratePerHour: 0, capacity: 4200, isFull: false, fullAt: null },
          ],
        }),
      ),
    });
    renderPage('/planet/storage', { auth: signedIn(), planets });

    const block = await screen.findByRole('region', { name: 'Ресурси' });
    const iron = within(block).getByText('Залізо').closest('tr')!;
    const copper = within(block).getByText('Мідь').closest('tr')!;

    expect(within(iron).getByText('Повний')).toBeInTheDocument();
    expect(within(iron).getByText('4 200 / 4 200')).toBeInTheDocument();
    expect(within(copper).queryByText('Повний')).not.toBeInTheDocument();
    expect(within(copper).getByText('0 / 4 200')).toBeInTheDocument();
    // The alarm stands above the tabs with the advice and the way to the construction.
    const alarm = screen.getByText(/^Склад заповнений, видобуток зупинено: Залізо\. Збудуйте більше місткості\./);
    expect(within(alarm).getByRole('link', { name: 'До будівництва' })).toHaveAttribute('href', '/planet/build');
  });

  it('switches the tabs and shows only the block of the chosen one', async () => {
    const user = userEvent.setup();
    renderPage('/planet/storage', { auth: signedIn() });

    await screen.findByRole('region', { name: 'Ресурси' });

    await user.click(screen.getByRole('tab', { name: 'Ресурси' }));
    expect(screen.getByRole('tab', { name: 'Ресурси', selected: true })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Ресурси' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Корпуси кораблів' })).not.toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Модулі кораблів' })).not.toBeInTheDocument();

    await user.click(screen.getByRole('tab', { name: 'Модулі' }));
    expect(await screen.findByRole('table', { name: 'Модулі' })).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Ресурси' })).not.toBeInTheDocument();

    await user.click(screen.getByRole('tab', { name: 'Усе' }));
    expect(await screen.findByRole('region', { name: 'Ресурси' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Артефакти' })).toBeInTheDocument();
  });

  it('moves between the tabs with the arrow keys', async () => {
    const user = userEvent.setup();
    renderPage('/planet/storage', { auth: signedIn() });

    const first = await screen.findByRole('tab', { name: 'Усе' });
    first.focus();
    await user.keyboard('{ArrowRight}');

    expect(screen.getByRole('tab', { name: 'Ресурси', selected: true })).toHaveFocus();

    await user.keyboard('{ArrowLeft}{ArrowLeft}');
    expect(screen.getByRole('tab', { name: 'Артефакти', selected: true })).toHaveFocus();
  });

  it('shows the parts of the stock with the group and the quantity', async () => {
    const user = userEvent.setup();
    const production = fakeProductionApi();
    renderPage('/planet/storage', { auth: signedIn(), production });

    await user.click(await screen.findByRole('tab', { name: 'Модулі' }));

    const table = await screen.findByRole('table', { name: 'Модулі' });
    const row = within(table).getByText('Корпус «Оса» Мк. II').closest('tr')!;

    expect(within(row).getByText('Корпусні модулі')).toBeInTheDocument();
    expect(within(row).getByText('4')).toBeInTheDocument();
    // The kind of the part has its own room: its parts against the capacity of the kind.
    expect(within(row).getByText('4 / 100')).toBeInTheDocument();
    expect(within(row).queryByText('Повний')).not.toBeInTheDocument();
    expect(production.stock).toHaveBeenCalledWith(10);
  });

  it('marks a full kind of part with the badge against its own room', async () => {
    const user = userEvent.setup();
    const production = fakeProductionApi({
      stock: vi.fn().mockResolvedValue(sampleProductionStock({ storage: { capacity: 4, kinds: [{ partTypeId: 3, quantity: 4, capacity: 4, isFull: true }] } })),
    });
    renderPage('/planet/storage', { auth: signedIn(), production });

    await user.click(await screen.findByRole('tab', { name: 'Модулі' }));

    const row = within(await screen.findByRole('table', { name: 'Модулі' })).getByText('Корпус «Оса» Мк. II').closest('tr')!;

    expect(within(row).getByText('Повний')).toBeInTheDocument();
    expect(within(row).getByText('4 / 4')).toBeInTheDocument();
  });

  it('says there are no parts yet', async () => {
    const user = userEvent.setup();
    const production = fakeProductionApi({ stock: vi.fn().mockResolvedValue(sampleProductionStock({ stacks: [] })) });
    renderPage('/planet/storage', { auth: signedIn(), production });

    await user.click(await screen.findByRole('tab', { name: 'Модулі' }));

    expect(await screen.findByText('На складі ще немає деталей.')).toBeInTheDocument();
  });

  it('says the stock of the parts did not load and keeps the rest of the screen', async () => {
    const production = fakeProductionApi({ stock: vi.fn().mockRejectedValue(new Error('down')) });
    renderPage('/planet/storage', { auth: signedIn(), production });

    expect(await screen.findByText('Не вдалося завантажити склад деталей.')).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Ресурси' })).toBeInTheDocument();
  });

  it('shows the placeholders with the word Soon, the reason and no made-up numbers', async () => {
    const user = userEvent.setup();
    renderPage('/planet/storage', { auth: signedIn() });

    for (const [tab, name] of [
      ['Корпуси', 'Корпуси кораблів'],
      ['Артефакти', 'Артефакти'],
    ]) {
      await user.click(await screen.findByRole('tab', { name: tab }));

      const block = screen.getByRole('region', { name });

      expect(within(block).getByText('Скоро')).toBeInTheDocument();
      expect(within(block).getByRole('button', { name: 'Недоступно' })).toBeDisabled();
      expect(within(block).getByRole('button', { name: 'Недоступно' })).toHaveAttribute('title', expect.stringMatching(/\S{10,}/));
      // The block has no table and no digit: nothing in it can be taken for data.
      expect(within(block).queryByRole('table')).not.toBeInTheDocument();
      expect(block.textContent).not.toMatch(/\d/);
    }
  });

  it('shows the loading state while the body is not here yet', async () => {
    renderPage('/planet/storage', { auth: signedIn(), planets: fakePlanetApi({ planet: vi.fn().mockReturnValue(new Promise(() => undefined)) }) });

    await screen.findByRole('tablist', { name: 'Розділи складу' });

    expect((await screen.findAllByRole('status'))[0]).toHaveTextContent('Завантаження');
    expect(screen.queryByRole('region', { name: 'Ресурси' })).not.toBeInTheDocument();
  });

  it('shows the error with a retry when the body did not load', async () => {
    const user = userEvent.setup();
    let down = true;
    const planet = vi.fn().mockImplementation(() => (down ? Promise.reject(new Error('down')) : Promise.resolve(samplePlanet())));
    renderPage('/planet/storage', { auth: signedIn(), planets: fakePlanetApi({ planet }) });

    expect(await screen.findByText('Не вдалося завантажити планету.')).toBeInTheDocument();
    down = false;
    await user.click(screen.getByRole('button', { name: 'Спробувати ще раз' }));

    expect(await screen.findByRole('region', { name: 'Ресурси' })).toBeInTheDocument();
  });

  it('says there are no resources when the body has none', async () => {
    renderPage('/planet/storage', { auth: signedIn(), planets: fakePlanetApi({ planet: vi.fn().mockResolvedValue(samplePlanet({ resources: [] })) }) });

    expect(await screen.findByText('На планеті ще немає ресурсів.')).toBeInTheDocument();
  });

  it('draws only the common frame and marks the storage as the current section', async () => {
    const { container } = renderPage('/planet/storage', { auth: signedIn() });

    await screen.findByRole('tablist', { name: 'Розділи складу' });

    expect(container.querySelectorAll('header')).toHaveLength(1);
    expect(container.querySelectorAll('.civ-side')).toHaveLength(1);
    await waitFor(() => expect(within(container.querySelector<HTMLElement>('.civ-side')!).getByRole('link', { current: 'page' })).toHaveTextContent('Склад'));
  });

  it('speaks English', async () => {
    window.localStorage.setItem(LANGUAGE_STORAGE_KEY, 'en');
    renderPage('/planet/storage', { auth: signedIn() });

    const tabs = await screen.findByRole('tablist', { name: 'Sections of the storage' });

    expect(within(tabs).getAllByRole('tab').map((tab) => tab.textContent)).toEqual(['All', 'Resources', 'Production', 'Modules', 'Hulls', 'Artifacts']);
    expect(await screen.findByRole('heading', { level: 1, name: 'Storage: Nova' })).toBeInTheDocument();
  });
});
