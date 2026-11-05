import { act, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Me } from '../../api/authApi';
import { ApiContext, type Api } from '../../api/context';
import { mockContentApi } from '../../api/mockContentApi';
import { ApiError } from '../../api/http';
import { serverConfig } from '../../config/server';
import { fakeAuthApi, fakeEconomyApi, fakeFleetApi, fakeGameApi, fakeMapApi, fakeMarketApi, fakePlanetApi, fakeProcessesApi, fakeProductionApi, fakeResearchApi, fakeTextsApi, fakeWorldApi, signedInMe } from '../../test/renderPage';
import { SessionProvider } from '../SessionProvider';
import { useSession } from '../useSession';

function Status() {
  const { session } = useSession();

  return <p>{session.status}</p>;
}

function renderSession(me: () => Promise<Me | null>) {
  const api: Api = { auth: fakeAuthApi({ me }), content: mockContentApi, economy: fakeEconomyApi(), fleet: fakeFleetApi(), game: fakeGameApi(), map: fakeMapApi(), market: fakeMarketApi(), planets: fakePlanetApi(), processes: fakeProcessesApi(), production: fakeProductionApi(), research: fakeResearchApi(), texts: fakeTextsApi(), world: fakeWorldApi() };

  return render(
    <ApiContext.Provider value={api}>
      <SessionProvider>
        <Status />
      </SessionProvider>
    </ApiContext.Provider>,
  );
}

describe('SessionProvider: a server that does not answer is not a player who is not signed in', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('stays in the loading state while the server is down and finds the player once it answers', async () => {
    const me = vi.fn().mockRejectedValueOnce(new ApiError(0, 'network')).mockRejectedValueOnce(new ApiError(500, 'server_error')).mockResolvedValue(signedInMe());

    renderSession(me);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });

    expect(screen.getByText('loading')).toBeInTheDocument();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(serverConfig.retryDelaysMs[0]);
    });

    expect(screen.getByText('loading')).toBeInTheDocument();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(serverConfig.retryDelaysMs[1]);
    });

    expect(screen.getByText('signed-in')).toBeInTheDocument();
    expect(me).toHaveBeenCalledTimes(3);
  });

  it('makes the session anonymous at once when the server says nobody is signed in', async () => {
    const me = vi.fn().mockResolvedValue(null);

    renderSession(me);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });

    expect(screen.getByText('anonymous')).toBeInTheDocument();
    expect(me).toHaveBeenCalledTimes(1);
  });

  it('asks no more once the page is gone', async () => {
    const me = vi.fn().mockRejectedValue(new ApiError(0, 'network'));
    const view = renderSession(me);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });
    view.unmount();
    await act(async () => {
      await vi.advanceTimersByTimeAsync(120_000);
    });

    expect(me).toHaveBeenCalledTimes(1);
  });
});
