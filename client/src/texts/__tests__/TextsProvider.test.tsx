import { act, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ApiContext, type Api } from '../../api/context';
import { mockContentApi } from '../../api/mockContentApi';
import type { TextsApi } from '../../api/textsApi';
import { I18nProvider } from '../../i18n/I18nProvider';
import { fakeAuthApi, fakeGameApi, fakePlanetApi, fakeResearchApi } from '../../test/renderPage';
import { TextsProvider } from '../TextsProvider';
import { useTexts } from '../useTexts';

function Names({ keys }: { keys: string[] }) {
  const { name } = useTexts();

  return (
    <ul>
      {keys.map((key) => (
        <li key={key}>{name(key)}</li>
      ))}
    </ul>
  );
}

function renderTexts(texts: TextsApi, keys: string[], language: 'uk' | 'en' = 'en') {
  const api: Api = { auth: fakeAuthApi(), content: mockContentApi, game: fakeGameApi(), planets: fakePlanetApi(), research: fakeResearchApi(), texts };

  return render(
    <I18nProvider initialLanguage={language}>
      <ApiContext.Provider value={api}>
        <TextsProvider>
          <Names keys={keys} />
        </TextsProvider>
      </ApiContext.Provider>
    </I18nProvider>,
  );
}

describe('TextsProvider (task 00022)', () => {
  it('shows the language, then the fallback language, then the key', async () => {
    const texts: TextsApi = {
      pack: vi.fn((language: string) =>
        Promise.resolve({
          language,
          version: 1,
          texts: (language === 'en'
            ? { 'world.resource.resource_1.name': 'Iron' }
            : { 'world.resource.resource_1.name': 'Залізо', 'world.resource.resource_2.name': 'Титан' }) as Record<string, string>,
        }),
      ),
    };

    renderTexts(texts, ['world.resource.resource_1.name', 'world.resource.resource_2.name', 'world.resource.resource_9.name']);

    expect(await screen.findByText('Iron')).toBeInTheDocument();
    expect(await screen.findByText('Титан')).toBeInTheDocument();
    expect(screen.getByText('world.resource.resource_9.name')).toBeInTheDocument();
    expect(texts.pack).toHaveBeenCalledWith('en');
    expect(texts.pack).toHaveBeenCalledWith('uk');
  });

  it('asks again when the page comes back, so a renamed resource shows without a restart', async () => {
    let name = 'Залізо';
    const texts: TextsApi = {
      pack: vi.fn((language: string) => Promise.resolve({ language, version: 1, texts: { 'world.resource.resource_1.name': name } })),
    };

    renderTexts(texts, ['world.resource.resource_1.name'], 'uk');
    expect(await screen.findByText('Залізо')).toBeInTheDocument();

    name = 'Ферум';
    act(() => {
      window.dispatchEvent(new Event('focus'));
    });

    await waitFor(() => expect(screen.getByText('Ферум')).toBeInTheDocument());
  });
});
