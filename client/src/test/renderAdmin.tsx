import { render, type RenderResult } from '@testing-library/react';
import AdminApp from '../admin/AdminApp';
import type { AdminApi } from '../admin/adminApi';
import type { ReferenceApi } from '../admin/referenceApi';
import { ApiContext, type Api } from '../api/context';
import { mockContentApi } from '../api/mockContentApi';
import type { TextsApi } from '../api/textsApi';
import { I18nProvider } from '../i18n/I18nProvider';
import { RouterProvider } from '../router/Router';
import { TextsProvider } from '../texts/TextsProvider';
import { fakeAuthApi, fakeGameApi, fakePlanetApi, fakeProductionApi, fakeResearchApi, fakeTextsApi } from './renderPage';

/**
 * The texts of the game data the admin tests show, by the neutral keys of the database.
 */
export const adminTexts: Record<string, Record<string, string>> = {
  uk: {
    'world.resource.resource_1.name': 'Залізо',
    'world.resource.resource_5.name': 'Кварцит',
    'world.resource_group.resource_group_1.name': 'Видобувні',
    'world.resource_group.resource_group_2.name': 'Мінерали',
    'world.object_type.object_type_11.name': 'Звичайне поле',
    'world.object_type.object_type_5.name': 'Землеподібна',
    'world.size_class.size_class_1.name': 'Мала',
  },
  en: {},
};

/**
 * Renders the admin panel at an address, with the texts of the game data from a fake server and,
 * when given, the endpoints of the reference kinds (task 00023).
 */
export function renderAdmin(
  path: string,
  api: AdminApi,
  texts: TextsApi = fakeTextsApi(adminTexts),
  references?: ReferenceApi,
): RenderResult & { texts: TextsApi } {
  window.history.replaceState(null, '', path);

  const site: Api = { auth: fakeAuthApi(), content: mockContentApi, game: fakeGameApi(), planets: fakePlanetApi(), production: fakeProductionApi(), research: fakeResearchApi(), texts };
  const result = render(
    <I18nProvider initialLanguage="uk">
      <ApiContext.Provider value={site}>
        <TextsProvider>
          <RouterProvider>
            <AdminApp api={api} references={references} />
          </RouterProvider>
        </TextsProvider>
      </ApiContext.Provider>
    </I18nProvider>,
  );

  return { ...result, texts };
}
