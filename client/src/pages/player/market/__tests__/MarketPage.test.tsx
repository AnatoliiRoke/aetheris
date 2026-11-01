import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../../api/http';
import { LANGUAGE_STORAGE_KEY } from '../../../../i18n/I18nProvider';
import {
  fakeAuthApi,
  fakeEconomyApi,
  fakeMarketApi,
  renderPage,
  sampleBalance,
  sampleEconomyOverview,
  sampleMarketLot,
  sampleMarketOverview,
  sampleMyLot,
  signedInMe,
} from '../../../../test/renderPage';
import type { Api } from '../../../../api/context';

/**
 * Makes the screen think it is a phone: the media query of the phone matches.
 */
function usePhone() {
  vi.spyOn(window, 'matchMedia').mockImplementation(
    (query: string) =>
      ({
        matches: query.includes('max-width: 767px'),
        media: query,
        onchange: null,
        addEventListener: () => undefined,
        removeEventListener: () => undefined,
        addListener: () => undefined,
        removeListener: () => undefined,
        dispatchEvent: () => false,
      }) as MediaQueryList,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

/**
 * Opens the screen of the market as a signed-in player with the fake interfaces of the test.
 */
function renderMarket(options: Partial<Pick<Api, 'market' | 'economy'>> = {}) {
  return renderPage('/market', { auth: fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) }), ...options });
}

const richEconomy = () =>
  fakeEconomyApi({
    overview: vi.fn().mockResolvedValue(
      sampleEconomyOverview({ balances: [sampleBalance({ resourceTypeId: 18, amount: 5000 }), sampleBalance({ resourceTypeId: 19, amount: 10000 })] }),
    ),
  });

describe('the screen of the market: the frame and the levels', () => {
  it('opens at /market with the title, the levels and the six tabs', async () => {
    renderMarket({ market: fakeMarketApi() });

    expect(await screen.findByRole('heading', { level: 1, name: 'Ринок' })).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: /^Сектор$/ })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByText('Комісія 5%')).toBeInTheDocument();

    for (const name of ['Купити', 'Продати', 'Мої лоти', 'Доставки', 'Обмін', 'Історія']) {
      expect(screen.getByRole('tab', { name: new RegExp(`^${name}`) })).toBeInTheDocument();
    }

    expect(screen.getByRole('link', { name: 'Ринок' })).toHaveAttribute('aria-current', 'page');
  });

  it('shows the clan level as closed with the reason in its hint', async () => {
    renderMarket({ market: fakeMarketApi() });

    const clan = await screen.findByRole('button', { name: /Клан: Ви ще не в клані/ });

    expect(clan).toBeDisabled();
    expect(clan).toHaveAttribute('aria-disabled', 'true');
  });

  it('reads the lots of the level that is picked', async () => {
    const user = userEvent.setup();
    const market = fakeMarketApi();
    renderMarket({ market });

    await user.click(await screen.findByRole('button', { name: 'Галактика' }));

    await waitFor(() => expect(market.lots).toHaveBeenLastCalledWith(expect.objectContaining({ marketLevelId: 3 })));
    expect(screen.getByText('Комісія 10%')).toBeInTheDocument();
    expect(screen.getByText('Галактичний ринок · ціни в торговій валюті')).toBeInTheDocument();
  });
});

describe('the tab Buy', () => {
  it('lists the lots with the seller, the delivery time and the fee of the delivery', async () => {
    renderMarket({ market: fakeMarketApi() });

    const table = await screen.findByRole('table', { name: 'Лоти для купівлі' });

    expect(within(table).getByText('Залізо')).toBeInTheDocument();
    expect(within(table).getByText('Vektor_Prime')).toBeInTheDocument();
    expect(within(table).getByText('12 хв')).toBeInTheDocument();
    expect(within(table).getByRole('button', { name: /^Купити: Залізо × 1.200$/ })).toBeEnabled();
  });

  it('shows a lot with the price of zero as a free transfer', async () => {
    const market = fakeMarketApi({
      lots: vi.fn().mockResolvedValue({ items: [sampleMarketLot({ pricePerUnit: 0, totalPrice: 0 })], energyRodBalance: 8, destinationHasPortal: true }),
    });
    renderMarket({ market });

    expect(await screen.findByText('Безкоштовно (передача)')).toBeInTheDocument();
  });

  it('gives the reason of the server on a button that cannot buy', async () => {
    const market = fakeMarketApi({
      lots: vi.fn().mockResolvedValue({
        items: [
          sampleMarketLot({ id: 1, unavailableReason: 'market_own_lot' }),
          sampleMarketLot({ id: 2, unavailableReason: 'market_trade_currency_short' }),
          sampleMarketLot({ id: 3, unavailableReason: 'market_energy_short' }),
          sampleMarketLot({ id: 4, unavailableReason: 'market_portal_missing' }),
        ],
        energyRodBalance: 0,
        destinationHasPortal: true,
      }),
    });
    renderMarket({ market });

    const disabled = (await screen.findAllByRole('button', { name: /^Купити: Залізо/ })).filter((button) => (button as HTMLButtonElement).disabled);

    expect(disabled.map((button) => button.getAttribute('title'))).toEqual([
      'Це ваш лот',
      'Недостатньо торгової валюти',
      'Недостатньо енергостержнів',
      'На обраній планеті немає порталу',
    ]);
  });

  it('asks the server for the items that match the search and tells when nothing is left', async () => {
    const user = userEvent.setup();
    const market = fakeMarketApi();
    renderMarket({ market });

    await screen.findByRole('table', { name: 'Лоти для купівлі' });
    await user.type(screen.getByRole('textbox', { name: 'Пошук товару' }), 'мідь');

    await waitFor(() => expect(market.lots).toHaveBeenLastCalledWith(expect.objectContaining({ resourceTypeIds: [3] })));

    await user.clear(screen.getByRole('textbox', { name: 'Пошук товару' }));
    await user.type(screen.getByRole('textbox', { name: 'Пошук товару' }), 'такого немає');

    expect(await screen.findByText('Немає лотів за вашими фільтрами.')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Скинути фільтри' }));

    expect(await screen.findByRole('table', { name: 'Лоти для купівлі' })).toBeInTheDocument();
  });

  it('sends the range of the price and the kind of the item to the server', async () => {
    const user = userEvent.setup();
    const market = fakeMarketApi();
    renderMarket({ market });

    await screen.findByRole('table', { name: 'Лоти для купівлі' });
    await user.type(screen.getByRole('textbox', { name: 'від' }), '50');
    await user.type(screen.getByRole('textbox', { name: 'до' }), '90');

    await waitFor(() => expect(market.lots).toHaveBeenLastCalledWith(expect.objectContaining({ minPrice: 50, maxPrice: 90 })));

    await user.click(screen.getByRole('button', { name: 'Деталі' }));

    await waitFor(() => expect(market.lots).toHaveBeenLastCalledWith(expect.objectContaining({ resourceTypeIds: [], partTypeIds: [3] })));
  });

  it('sorts on the server by a column and tells the direction', async () => {
    const user = userEvent.setup();
    const market = fakeMarketApi();
    renderMarket({ market });

    await screen.findByRole('table', { name: 'Лоти для купівлі' });
    await user.click(screen.getByRole('button', { name: /Сортувати: Ціна за од\./ }));

    await waitFor(() => expect(market.lots).toHaveBeenLastCalledWith(expect.objectContaining({ sort: 'price', descending: false, offset: 0 })));
    expect(screen.getByRole('columnheader', { name: /Ціна за од\./ })).toHaveAttribute('aria-sort', 'ascending');

    await user.click(screen.getByRole('button', { name: /Сортувати: Ціна за од\./ }));

    await waitFor(() => expect(market.lots).toHaveBeenLastCalledWith(expect.objectContaining({ sort: 'price', descending: true })));
  });

  it('sorts by the name of the item in the order of the names', async () => {
    const user = userEvent.setup();
    const market = fakeMarketApi();
    renderMarket({ market });

    await screen.findByRole('table', { name: 'Лоти для купівлі' });
    await user.click(screen.getByRole('button', { name: /Сортувати: Товар/ }));

    await waitFor(() => expect(market.lots).toHaveBeenLastCalledWith(expect.objectContaining({ sort: 'item', itemOrder: expect.arrayContaining(['r1', 'r3']) })));
  });

  it('moves between the pages of a long list', async () => {
    const user = userEvent.setup();
    const market = fakeMarketApi({
      lots: vi.fn().mockResolvedValue({ items: [sampleMarketLot()], energyRodBalance: 8, destinationHasPortal: true, total: 250, offset: 0, limit: 40 }),
    });
    renderMarket({ market });

    expect(await screen.findByText('1–40 з 250')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Назад' })).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'Далі' }));

    await waitFor(() => expect(market.lots).toHaveBeenLastCalledWith(expect.objectContaining({ offset: 40 })));
  });

  it('groups the lots by item and opens the lots of one item', async () => {
    const user = userEvent.setup();
    const market = fakeMarketApi();
    renderMarket({ market });

    await screen.findByRole('table', { name: 'Лоти для купівлі' });
    await user.click(screen.getByRole('button', { name: 'За товаром' }));

    expect(await screen.findByText('Лотів: 3')).toBeInTheDocument();
    expect(market.lotGroups).toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: /Показати лоти: Залізо/ }));

    expect(await screen.findByRole('table', { name: 'Лоти для купівлі' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Товар: Залізо/ })).toBeInTheDocument();
    await waitFor(() => expect(market.lots).toHaveBeenLastCalledWith(expect.objectContaining({ resourceTypeIds: [1], partTypeIds: [] })));
  });

  it('warns that there is no portal on the chosen body', async () => {
    const market = fakeMarketApi({
      lots: vi.fn().mockResolvedValue({ items: [sampleMarketLot({ unavailableReason: 'market_portal_missing' })], energyRodBalance: 0, destinationHasPortal: false }),
    });
    renderMarket({ market });

    expect(await screen.findByText('На обраній планеті немає порталу — купувати поки не можна.')).toBeInTheDocument();
  });

  it('shows the error with the button that reads again, and the skeleton while it loads', async () => {
    const user = userEvent.setup();
    const lots = vi
      .fn()
      .mockRejectedValueOnce(new ApiError(500, 'internal_error'))
      .mockResolvedValue({ items: [sampleMarketLot()], energyRodBalance: 8, destinationHasPortal: true });
    renderMarket({ market: fakeMarketApi({ lots }) });

    expect(await screen.findByText('Не вдалося завантажити дані')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Спробувати знову' }));

    expect(await screen.findByRole('table', { name: 'Лоти для купівлі' })).toBeInTheDocument();
  });

  it('shows cards instead of the table on a phone', async () => {
    usePhone();
    renderMarket({ market: fakeMarketApi() });

    expect(await screen.findByRole('button', { name: 'Купити' })).toBeInTheDocument();
    expect(screen.queryByRole('table', { name: 'Лоти для купівлі' })).not.toBeInTheDocument();
    expect(screen.getByRole('group', { name: 'Сортування' })).toBeInTheDocument();
  });
});

describe('the purchase', () => {
  it('asks the server for the sum for the chosen body, buys once and tells the time', async () => {
    const user = userEvent.setup();
    const market = fakeMarketApi();
    renderMarket({ market, economy: richEconomy() });

    await user.click(await screen.findByRole('button', { name: /^Купити: Залізо × 1.200$/ }));

    const dialog = await screen.findByRole('dialog');

    await waitFor(() => expect(market.quote).toHaveBeenCalledWith(101, 10));
    expect(within(dialog).getByText('Енергостержні до сплати')).toBeInTheDocument();
    expect(await within(dialog).findByText('12 хв')).toBeInTheDocument();

    await user.click(within(dialog).getByRole('button', { name: 'Підтвердити' }));

    await waitFor(() => expect(market.buy).toHaveBeenCalledWith(101, 10));
    expect(market.buy).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(await screen.findByText('Покупку підтверджено — товар прибуде за 12 хв.')).toBeInTheDocument();
  });

  it('lists a body without a portal as closed and keeps the reason of a refusal in the dialog', async () => {
    const user = userEvent.setup();
    const market = fakeMarketApi({ buy: vi.fn().mockRejectedValue(new ApiError(409, 'market_lot_unavailable')) });
    renderMarket({ market, economy: richEconomy() });

    await user.click(await screen.findByRole('button', { name: /^Купити: Залізо × 1.200$/ }));

    const dialog = await screen.findByRole('dialog');
    await within(dialog).findByText('12 хв');
    await user.click(within(dialog).getByRole('button', { name: 'Підтвердити' }));

    expect(await within(dialog).findByText('Лот уже продано або скасовано')).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('closes with Escape and with the button', async () => {
    const user = userEvent.setup();
    renderMarket({ market: fakeMarketApi(), economy: richEconomy() });

    await user.click(await screen.findByRole('button', { name: /^Купити: Залізо × 1.200$/ }));
    await screen.findByRole('dialog');
    await user.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });
});

describe('the tab Sell', () => {
  it('shows the range from the server and the counter of the lots, and lists the lot', async () => {
    const user = userEvent.setup();
    const market = fakeMarketApi();
    renderMarket({ market });

    await user.click(await screen.findByRole('tab', { name: /^Продати/ }));

    expect(await screen.findByText('Дозволений діапазон: 80–120 за одиницю')).toBeInTheDocument();
    expect(screen.getByText('1 / 3')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByLabelText(/^Ціна за одиницю/)).toHaveValue('100'));

    await user.click(screen.getByRole('button', { name: 'Виставити' }));

    await waitFor(() => expect(market.listLot).toHaveBeenCalledWith(expect.objectContaining({ planetId: 10, marketLevelId: 2, resourceTypeId: 1, pricePerUnit: 100 })));
    expect(await screen.findByText('Лот виставлено на ринок.')).toBeInTheDocument();
  });

  it('refuses a price outside the range and allows the price of zero', async () => {
    const user = userEvent.setup();
    renderMarket({ market: fakeMarketApi() });

    await user.click(await screen.findByRole('tab', { name: /^Продати/ }));

    const price = await screen.findByLabelText(/^Ціна за одиницю/);
    await waitFor(() => expect(price).toHaveValue('100'));
    await user.clear(price);
    await user.type(price, '500');

    expect(screen.getByText('Ціна поза дозволеним діапазоном')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Виставити' })).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'Ціна 0 передає товар безкоштовно' }));

    expect(screen.queryByText('Ціна поза дозволеним діапазоном')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Виставити' })).toBeEnabled();
  });

  it('counts the fee of the level in the summary', async () => {
    const user = userEvent.setup();
    renderMarket({ market: fakeMarketApi() });

    await user.click(await screen.findByRole('tab', { name: /^Продати/ }));
    await waitFor(() => expect(screen.getByLabelText(/^Ціна за одиницю/)).toHaveValue('100'));
    await user.clear(screen.getByLabelText('Кількість · доступно 900'));
    await user.type(screen.getByLabelText(/^Кількість/), '10');

    expect(screen.getByText('1 000 ТВ')).toBeInTheDocument();
    expect(screen.getByText('−50 ТВ')).toBeInTheDocument();
    expect(screen.getByText('950 ТВ')).toBeInTheDocument();
  });

  it('closes the form and leads to the construction when no body has a trade centre and a portal', async () => {
    const user = userEvent.setup();
    const market = fakeMarketApi({
      overview: vi.fn().mockResolvedValue(sampleMarketOverview({ planets: [{ planetId: 10, hasTradeCentre: false, hasPortal: false }] })),
    });
    renderMarket({ market });

    await user.click(await screen.findByRole('tab', { name: /^Продати/ }));

    expect((await screen.findAllByText('Потрібні Торговий центр і портал')).length).toBeGreaterThan(0);
    expect(screen.getByRole('link', { name: 'До будівництва' })).toHaveAttribute('href', '/planet/build');
    expect(screen.getByRole('button', { name: 'Виставити' })).toBeDisabled();
  });

  it('stops at the limit of the lots', async () => {
    const user = userEvent.setup();
    const market = fakeMarketApi({ overview: vi.fn().mockResolvedValue(sampleMarketOverview({ lotLimit: { used: 3, limit: 3 } })) });
    renderMarket({ market });

    await user.click(await screen.findByRole('tab', { name: /^Продати/ }));

    expect(await screen.findByRole('button', { name: 'Виставити' })).toBeDisabled();
    expect(screen.getByText('Ліміт лотів вичерпано')).toBeInTheDocument();
  });
});

describe('my lots, deliveries, exchange and history', () => {
  it('asks twice before it takes a lot off the market', async () => {
    const user = userEvent.setup();
    const market = fakeMarketApi();
    renderMarket({ market });

    await user.click(await screen.findByRole('tab', { name: /^Мої лоти/ }));
    await user.click(await screen.findByRole('button', { name: 'Скасувати лот' }));

    expect(market.cancelLot).not.toHaveBeenCalled();
    expect(screen.getByText('Скасувати лот? Товар повернеться на склад.')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Так, скасувати' }));

    await waitFor(() => expect(market.cancelLot).toHaveBeenCalledWith(201));
    expect(await screen.findByText('Лот скасовано, товар повернуто на склад.')).toBeInTheDocument();
  });

  it('tells that there are no lots', async () => {
    const user = userEvent.setup();
    renderMarket({ market: fakeMarketApi({ myLots: vi.fn().mockResolvedValue([]) }) });

    await user.click(await screen.findByRole('tab', { name: /^Мої лоти/ }));

    expect(await screen.findByText('У вас немає активних лотів.')).toBeInTheDocument();
  });

  it('shows the deliveries with the timer and the state', async () => {
    const user = userEvent.setup();
    const incoming = {
      id: 1,
      counterparty: { playerId: 'u2', nickname: 'Nexa_Lumen', raceNameKey: null, raceColorKey: null },
      sourcePlanetId: 20,
      destinationPlanetId: 10,
      resourceTypeId: 1,
      partTypeId: null,
      quantity: 8,
      energyRodAmount: 2,
      startedAt: '2026-10-26T11:50:00Z',
      arrivesAt: '2026-10-26T12:10:00Z',
      state: 'travelling',
    };
    const market = fakeMarketApi({
      deliveries: vi.fn().mockResolvedValue({ incoming: [incoming, { ...incoming, id: 2, state: 'arrived', arrivesAt: '2026-10-26T11:55:00Z' }], outgoing: [] }),
    });
    renderMarket({ market });

    await user.click(await screen.findByRole('tab', { name: /^Доставки/ }));

    expect(await screen.findByText('В дорозі')).toBeInTheDocument();
    expect(screen.getByText('Доставлено')).toBeInTheDocument();
    expect(screen.getByText(/залишилось/)).toBeInTheDocument();
    expect(screen.getAllByText('Nexa_Lumen → Nova')).toHaveLength(2);
  });

  it('exchanges the credits for the trade currency at the rate of the server', async () => {
    const user = userEvent.setup();
    const market = fakeMarketApi();
    renderMarket({ market, economy: richEconomy() });

    await user.click(await screen.findByRole('tab', { name: /^Обмін/ }));

    expect(await screen.findByText('Курс: 1 кредит = 0,01 торгової валюти')).toBeInTheDocument();
    expect(screen.getAllByText('Комісія 5%')).toHaveLength(2);

    await user.click(screen.getByRole('button', { name: 'Обміняти' }));

    await waitFor(() => expect(market.exchange).toHaveBeenCalledWith(18, 1000));
    expect(await screen.findByText('Обмін виконано.')).toBeInTheDocument();
  });

  it('does not exchange more than the player has', async () => {
    const user = userEvent.setup();
    const market = fakeMarketApi();
    renderMarket({ market, economy: richEconomy() });

    await user.click(await screen.findByRole('tab', { name: /^Обмін/ }));

    const amount = await screen.findByLabelText(/^Сума/);
    await user.clear(amount);
    await user.type(amount, '999999');

    expect(screen.getByText('Недостатньо коштів')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Обміняти' })).toBeDisabled();

    await user.click(screen.getByRole('button', { name: 'Торгова валюта → Кредити' }));

    expect(screen.getByRole('button', { name: 'Торгова валюта → Кредити' })).toHaveAttribute('aria-pressed', 'true');
  });

  it('draws the chart of the rate from the points of the server and tells when there are none', async () => {
    const user = userEvent.setup();
    const market = fakeMarketApi({
      rateHistory: vi.fn().mockResolvedValue({
        points: [
          { day: '2026-10-25T00:00:00Z', rate: 100 },
          { day: '2026-10-26T00:00:00Z', rate: 120 },
        ],
        dayChangePercent: 20,
      }),
    });
    renderMarket({ market });

    await user.click(await screen.findByRole('tab', { name: /^Обмін/ }));

    expect(await screen.findByRole('img', { name: /Курс за дні/ })).toBeInTheDocument();
  });

  it('shows the history of the deals with the direction in words', async () => {
    const user = userEvent.setup();
    const market = fakeMarketApi({
      deals: vi.fn().mockResolvedValue({
        total: 1,
        offset: 0,
        limit: 40,
        items: [
        {
          id: 1,
          direction: 'bought',
          counterparty: { playerId: 'u2', nickname: 'Vektor_Prime', raceNameKey: null, raceColorKey: null },
          resourceTypeId: 1,
          partTypeId: null,
          quantity: 500,
          pricePerUnit: 4,
          sum: 2000,
          commission: 100,
          createdAt: '2026-10-26T14:32:00Z',
        },
        ],
      }),
    });
    renderMarket({ market });

    await user.click(await screen.findByRole('tab', { name: /^Історія/ }));

    expect(await screen.findByText('Куплено')).toBeInTheDocument();
    expect(screen.getByText('26.10 14:32')).toBeInTheDocument();
    expect(screen.getByText('Vektor_Prime')).toBeInTheDocument();
  });

  it('moves between the pages of the history, forty deals on a page', async () => {
    const user = userEvent.setup();
    const deal = {
      id: 1,
      direction: 'sold',
      counterparty: { playerId: 'u2', nickname: 'Vektor_Prime', raceNameKey: null, raceColorKey: null },
      resourceTypeId: 1,
      partTypeId: null,
      quantity: 5,
      pricePerUnit: 4,
      sum: 20,
      commission: 1,
      createdAt: '2026-10-26T14:32:00Z',
    };
    const market = fakeMarketApi({ deals: vi.fn().mockResolvedValue({ items: [deal], total: 100, offset: 0, limit: 40 }) });
    renderMarket({ market });

    await user.click(await screen.findByRole('tab', { name: /^Історія/ }));

    expect(await screen.findByText('1–40 з 100')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Далі' }));

    await waitFor(() => expect(market.deals).toHaveBeenLastCalledWith(40));
  });

  it('tells that there are no deals yet', async () => {
    const user = userEvent.setup();
    renderMarket({ market: fakeMarketApi() });

    await user.click(await screen.findByRole('tab', { name: /^Історія/ }));

    expect(await screen.findByText('Угод ще не було.')).toBeInTheDocument();
  });
});

describe('the language and the data', () => {
  it('writes the screen in English when the player picks English', async () => {
    window.localStorage.setItem(LANGUAGE_STORAGE_KEY, 'en');
    renderMarket({ market: fakeMarketApi() });

    expect(await screen.findByRole('heading', { level: 1, name: 'Market' })).toBeInTheDocument();
    expect(await screen.findByRole('tab', { name: 'Sell' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Sector' })).toBeInTheDocument();
    expect(screen.getByText('Fee 5%')).toBeInTheDocument();
  });

  it('reads every number from the server: no lot is made up when the server has none', async () => {
    const market = fakeMarketApi({
      lots: vi.fn().mockResolvedValue({ items: [], energyRodBalance: 0, destinationHasPortal: true, total: 0, offset: 0, limit: 40 }),
      myLots: vi.fn().mockResolvedValue([sampleMyLot()]),
    });
    renderMarket({ market });

    expect(await screen.findByText('Немає лотів за вашими фільтрами.')).toBeInTheDocument();
    expect(screen.queryByText('Vektor_Prime')).not.toBeInTheDocument();
  });
});
