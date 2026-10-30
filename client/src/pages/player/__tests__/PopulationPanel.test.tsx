import { screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../../api/http';
import { fakeAuthApi, fakeEconomyApi, renderPage, sampleBalance, samplePopulation, signedInMe } from '../../../test/renderPage';

/**
 * A signed-in player with a civilization and a confirmed address.
 */
const signedIn = () => fakeAuthApi({ me: vi.fn().mockResolvedValue(signedInMe()) });

describe('PopulationPanel', () => {
  it('shows the population of the body with its growth and the time to the maximum', async () => {
    renderPage('/planet', { auth: signedIn() });

    const panel = await screen.findByRole('region', { name: 'Населення' });

    expect(await within(panel).findByText('750')).toBeInTheDocument();
    expect(within(panel).getByText('Зараз')).toBeInTheDocument();
    expect(within(panel).getByText('900')).toBeInTheDocument();
    expect(within(panel).getByText('+45')).toBeInTheDocument();
    expect(within(panel).getByText('0:03:20')).toBeInTheDocument();
  });

  it('warns when the population is above the limit', async () => {
    const economy = fakeEconomyApi({
      population: vi.fn().mockResolvedValue(samplePopulation({ amount: 950, maximum: 900, overLimit: true })),
    });

    renderPage('/planet', { auth: signedIn(), economy });

    const panel = await screen.findByRole('region', { name: 'Населення' });

    expect(await within(panel).findByText('Понад ліміт, вимирання')).toBeInTheDocument();
  });

  it('shows a placeholder when the body has no population yet', async () => {
    const economy = fakeEconomyApi({
      population: vi.fn().mockRejectedValue(new ApiError(404, 'not_found')),
    });

    renderPage('/planet', { auth: signedIn(), economy });

    const panel = await screen.findByRole('region', { name: 'Населення' });

    await waitFor(() => expect(within(panel).getAllByText('—')).toHaveLength(4));
    expect(within(panel).queryByText('750')).not.toBeInTheDocument();
  });

  it('reads the time to the maximum with the fraction of a second the server counts in ticks', async () => {
    const economy = fakeEconomyApi({
      population: vi.fn().mockResolvedValue(samplePopulation({ timeToMaximum: '19:39:59.9999999' })),
    });

    renderPage('/planet', { auth: signedIn(), economy });

    const panel = await screen.findByRole('region', { name: 'Населення' });

    expect(await within(panel).findByText('19:40:00')).toBeInTheDocument();
  });

  it('shows a real zero as a zero and not as a placeholder', async () => {
    const economy = fakeEconomyApi({
      population: vi.fn().mockResolvedValue(samplePopulation({ amount: 0, growthPerHour: 0, timeToMaximum: '00:00:00' })),
    });

    renderPage('/planet', { auth: signedIn(), economy });

    const panel = await screen.findByRole('region', { name: 'Населення' });

    // The population, the growth and the time are zero here and are shown as zeros; the placeholder
    // stays for the facts the server did not send.
    await waitFor(() => expect(within(panel).getAllByText('0')).toHaveLength(2));
    expect(within(panel).getByText('0:00:00')).toBeInTheDocument();
    expect(within(panel).queryByText('—')).not.toBeInTheDocument();
  });

  it('shows an empty balance of the credits as zero in the header of the game', async () => {
    const economy = fakeEconomyApi({
      overview: vi.fn().mockResolvedValue({
        populations: [],
        balances: [sampleBalance({ amount: 0, incomePerHour: 0 })],
        gameNow: '2026-10-26T12:00:00Z',
      }),
    });

    renderPage('/game', { auth: signedIn(), economy });

    const header = await screen.findByRole('banner');

    expect(await within(header).findByText('(0/год)')).toBeInTheDocument();

    const badge = within(header).getByText('Кредити').closest('span.badge');
    expect(badge?.getAttribute('title')).toContain('Кредити: 0');
  });

  it('shows the balance of the credits with the income per hour in the header of the game', async () => {
    renderPage('/game', { auth: signedIn() });

    const header = await screen.findByRole('banner');

    expect(await within(header).findByText('Кредити')).toBeInTheDocument();
    expect(within(header).getByText('234')).toBeInTheDocument();
    expect(within(header).getByText('(+25/год)')).toBeInTheDocument();

    // The full values stay in the hint of the badge.
    const badge = within(header).getByText('Кредити').closest('span.badge');
    expect(badge?.getAttribute('title')).toContain('Дохід за годину: +25');
  });

  it('shows a placeholder in the header when there are no balances yet', async () => {
    const economy = fakeEconomyApi({
      overview: vi.fn().mockResolvedValue({ populations: [], balances: [], gameNow: '2026-10-26T12:00:00Z' }),
    });

    renderPage('/game', { auth: signedIn(), economy });

    const header = await screen.findByRole('banner');

    await waitFor(() => expect(within(header).getByText('—')).toBeInTheDocument());
  });
});
