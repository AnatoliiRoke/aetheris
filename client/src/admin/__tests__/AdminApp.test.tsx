import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/http';
import type { AdminApi, AdminMe, ConfigParameter, GenerationEstimate, GenerationRun, Page, Row } from '../adminApi';
import { tableSearch } from '../adminApi';
import { renderAdmin } from '../../test/renderAdmin';
import { adminEn } from '../i18n/en';
import { adminUk } from '../i18n/uk';

const allPermissions = [
  'admin.me',
  'admin.sign_out',
  'admin.world_generation_estimated',
  'admin.world_generation_started',
  'admin.world_generation_run_read',
  'admin.world_generation_runs_read',
  'admin.world_sectors_read',
  'admin.world_systems_read',
  'admin.world_objects_read',
  'admin.world_fields_read',
  'admin.world_reference_read',
  'admin.config_read',
  'admin.config_history_read',
  'admin.config_changed',
  'admin.config_rolled_back',
  'admin.clock_read',
  'admin.audit_read',
];

function adminMe(overrides: Partial<AdminMe> = {}): AdminMe {
  return {
    admin_id: 'admin-1',
    display_name: 'o***@example.test',
    role_codes: ['owner'],
    permission_codes: allPermissions,
    idle_minutes: 30,
    second_factor: 'none',
    require_totp: false,
    ...overrides,
  };
}

function page<T>(items: T[], total = items.length): Page<T> {
  return { items, total, page: 1, page_size: 50 };
}

const estimate: GenerationEstimate = {
  sector_count: 2,
  estimated_system_count: 200,
  systems_min: 50,
  systems_max: 150,
  max_sectors_per_run: 10,
  max_systems_per_run: 1000,
  allowed: true,
  active_run_id: null,
  status_poll_seconds: 1,
  rule_sets: [{ code: 'current', rule_group_count: 4, rule_count: 20 }],
};

const finishedRun: GenerationRun = {
  id: 7,
  initiator_id: 'admin-1',
  sector_count: 2,
  seed: 42,
  rule_set: 'current',
  estimated_system_count: 200,
  status: 'succeeded',
  created_at: '2026-10-26T10:00:00Z',
  started_at: '2026-10-26T10:00:01Z',
  finished_at: '2026-10-26T10:00:05Z',
  summary: {
    sector_ids: [4, 5],
    sectors: [
      { sector_id: 4, seed: 42 },
      { sector_id: 5, seed: 99 },
    ],
    system_count: 190,
    object_counts_by_class: { planet: 900, gas_giant: 120, asteroid_field: 40 },
  },
  error: null,
};

function fakeAdminApi(overrides: Partial<AdminApi> = {}): AdminApi {
  return {
    login: vi.fn().mockResolvedValue(undefined),
    logout: vi.fn().mockResolvedValue(undefined),
    me: vi.fn().mockResolvedValue(adminMe()),
    table: vi.fn().mockResolvedValue(page<Row>([])),
    configHistory: vi.fn().mockResolvedValue(page([])),
    changeConfig: vi.fn().mockResolvedValue(undefined),
    rollbackConfig: vi.fn().mockResolvedValue(undefined),
    clock: vi.fn().mockResolvedValue({ multiplier: 1, max_multiplier: 100, read_only: true }),
    reference: vi.fn().mockResolvedValue({ resources: [], object_types: [], size_classes: [] }),
    estimate: vi.fn().mockResolvedValue(estimate),
    startGeneration: vi.fn().mockResolvedValue({ ...finishedRun, status: 'queued' }),
    run: vi.fn().mockResolvedValue(finishedRun),
    fieldCard: vi.fn(),
    updateField: vi.fn(),
    bodyCard: vi.fn(),
    updateBody: vi.fn(),
    systemCard: vi.fn(),
    updateSystem: vi.fn(),
    updateObjectType: vi.fn().mockResolvedValue(undefined),
    updateRule: vi.fn().mockResolvedValue(undefined),
    addRule: vi.fn().mockResolvedValue(undefined),
    updateRuleGroup: vi.fn().mockResolvedValue(undefined),
    textSettings: vi.fn().mockResolvedValue({ languages: ['uk', 'en'], max_length: 2000 }),
    changeText: vi.fn().mockResolvedValue(undefined),
    createResource: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  };
}

describe('admin texts', () => {
  it('has the same keys and parameters in both languages', () => {
    const parameters = (text: string) => [...text.matchAll(/\{(\w+)\}/g)].map((match) => match[1]).sort();

    expect(Object.keys(adminEn).sort()).toEqual(Object.keys(adminUk).sort());

    for (const key of Object.keys(adminUk) as (keyof typeof adminUk)[]) {
      expect(parameters(adminEn[key]), key).toEqual(parameters(adminUk[key]));
    }
  });

  it('builds the query of a table from the values that are set', () => {
    expect(tableSearch({ page: 2, search: 'S1', sort: 'name', direction: 'desc', filters: { sector_id: '3', owned: undefined } })).toBe(
      'page=2&search=S1&sort=name&direction=desc&sector_id=3',
    );
  });
});

describe('AdminApp', () => {
  it('asks a visitor to sign in and opens the panel after the sign-in', async () => {
    const user = userEvent.setup();
    const me = vi.fn().mockRejectedValueOnce(new ApiError(401, 'not_authenticated')).mockResolvedValue(adminMe());
    const api = fakeAdminApi({ me });
    renderAdmin('/admin', api);

    await user.type(await screen.findByLabelText('Email'), 'owner@example.test');
    await user.type(screen.getByLabelText('Пароль'), 'Violet-harbor-lantern-42');
    await user.click(screen.getByRole('button', { name: 'Увійти' }));

    expect(api.login).toHaveBeenCalledWith('owner@example.test', 'Violet-harbor-lantern-42', '');
    expect(await screen.findByRole('heading', { name: 'Генерація секторів' })).toBeInTheDocument();
    await waitFor(() => expect(window.location.pathname).toBe('/admin/generation'));
  });

  it('shows the error of a wrong code', async () => {
    const user = userEvent.setup();
    const api = fakeAdminApi({
      me: vi.fn().mockRejectedValue(new ApiError(401, 'not_authenticated')),
      login: vi.fn().mockRejectedValue(new ApiError(401, 'second_factor_required')),
    });
    renderAdmin('/admin', api);

    await user.type(await screen.findByLabelText('Email'), 'owner@example.test');
    await user.type(screen.getByLabelText('Пароль'), 'Violet-harbor-lantern-42');
    await user.click(screen.getByRole('button', { name: 'Увійти' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Потрібен правильний код автентифікатора.');
  });

  it('offers only the screens the role allows', async () => {
    const api = fakeAdminApi({ me: vi.fn().mockResolvedValue(adminMe({ permission_codes: ['admin.me', 'admin.config_read'] })) });
    renderAdmin('/admin/generation', api);

    const navigation = await screen.findByRole('navigation', { name: 'Розділи адмін-панелі' });

    expect(within(navigation).getByRole('link', { name: 'Параметри' })).toBeInTheDocument();
    expect(within(navigation).queryByRole('link', { name: 'Генерація' })).not.toBeInTheDocument();
    await waitFor(() => expect(window.location.pathname).toBe('/admin/config'));
  });

  it('confirms a generation with its summary and shows the result of the run', async () => {
    const user = userEvent.setup();
    const api = fakeAdminApi();
    renderAdmin('/admin/generation', api);

    const count = await screen.findByLabelText('Скільки секторів додати');
    await user.clear(count);
    await user.type(count, '2');
    await user.type(screen.getByLabelText('Seed'), '42');
    await user.click(screen.getByRole('button', { name: 'Далі' }));

    expect(await screen.findByText('Створити секторів: 2, приблизно 200 систем.')).toBeInTheDocument();
    expect(api.startGeneration).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: 'Запустити генерацію' }));

    expect(api.startGeneration).toHaveBeenCalledWith(2, 42, 'current');
    expect(await screen.findByText('Готово')).toBeInTheDocument();
    expect(screen.getByText('#4 (42), #5 (99)')).toBeInTheDocument();
    expect(screen.getByText('190')).toBeInTheDocument();
  });

  it('does not offer the start without the permission', async () => {
    const permissions = allPermissions.filter((code) => code !== 'admin.world_generation_started');
    renderAdmin('/admin/generation', fakeAdminApi({ me: vi.fn().mockResolvedValue(adminMe({ permission_codes: permissions })) }));

    expect(await screen.findByRole('button', { name: 'Далі' })).toBeDisabled();
  });

  it('searches and pages a table on the server', async () => {
    const user = userEvent.setup();
    const rows = Array.from({ length: 3 }, (_, index) => ({ id: index + 1, name: `S${index + 1}`, system_count: 50 }));
    const table = vi.fn().mockResolvedValue({ items: rows, total: 120, page: 1, page_size: 50 });
    renderAdmin('/admin/sectors', fakeAdminApi({ table }));

    expect(await screen.findByText('S2')).toBeInTheDocument();
    expect(screen.getByText('Сторінка 1 з 3')).toBeInTheDocument();

    await user.type(screen.getByRole('searchbox'), 'S2');
    await user.click(screen.getByRole('button', { name: 'Знайти' }));
    await waitFor(() => expect(table).toHaveBeenLastCalledWith('sectors', expect.objectContaining({ page: 1, search: 'S2' })));

    await user.click(screen.getByRole('button', { name: 'Далі' }));
    await waitFor(() => expect(table).toHaveBeenLastCalledWith('sectors', expect.objectContaining({ page: 2, search: 'S2' })));

    await user.click(screen.getByRole('button', { name: /Назва/ }));
    await waitFor(() => expect(table).toHaveBeenLastCalledWith('sectors', expect.objectContaining({ page: 1, sort: 'name', direction: 'asc' })));
  });

  it('changes a parameter only with a reason and offers the rollback', async () => {
    const user = userEvent.setup();
    const parameter: ConfigParameter = {
      key: 'world.admin.max_sectors_per_run',
      value: '10',
      type: 'integer',
      minimum: '1',
      maximum: '1000',
      default_value: '10',
      description: 'Largest number of sectors.',
      read_only: false,
      version: 1,
      updated_by: 'seed',
      updated_at: '2026-10-26T10:00:00Z',
    };
    const api = fakeAdminApi({
      table: vi.fn().mockResolvedValue(page<Row>([parameter as unknown as Row])),
      configHistory: vi.fn().mockResolvedValue(
        page([
          {
            id: 3,
            key: parameter.key,
            old_value: '5',
            new_value: '10',
            version: 2,
            reason: 'More room',
            rolled_back_change_id: null,
            admin_name: 'o***@example.test',
            changed_at: '2026-10-26T10:00:00Z',
          },
        ]),
      ),
    });
    renderAdmin('/admin/config', api);

    await user.click(await screen.findByRole('button', { name: 'Відкрити' }));
    const value = await screen.findByDisplayValue('10');
    await user.clear(value);
    await user.type(value, '12');
    await user.click(screen.getByRole('button', { name: 'Зберегти' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Вкажіть причину зміни.');
    expect(api.changeConfig).not.toHaveBeenCalled();

    await user.type(screen.getByPlaceholderText('Чому змінюєте значення'), 'Balance');
    await user.click(screen.getByRole('button', { name: 'Зберегти' }));
    expect(api.changeConfig).toHaveBeenCalledWith(parameter.key, '12', 'Balance');

    await user.type(screen.getByPlaceholderText('Чому змінюєте значення'), 'Back');
    await user.click(screen.getByRole('button', { name: 'Повернути «5»' }));
    expect(api.rollbackConfig).toHaveBeenCalledWith(parameter.key, 3, 'Back');
  });

  it('keeps a read-only parameter closed', async () => {
    const user = userEvent.setup();
    const parameter = {
      key: 'admin.require_totp',
      value: 'false',
      type: 'boolean',
      minimum: null,
      maximum: null,
      default_value: 'true',
      description: 'Second factor.',
      read_only: true,
      version: 1,
      updated_by: 'seed',
      updated_at: '2026-10-26T10:00:00Z',
    };
    renderAdmin('/admin/config', fakeAdminApi({ table: vi.fn().mockResolvedValue(page<Row>([parameter])) }));

    await user.click(await screen.findByRole('button', { name: 'Відкрити' }));

    expect(await screen.findByText('Небезпечний параметр: у панелі лише для читання.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Зберегти' })).toBeDisabled();
  });

  it('returns to the sign-in when the session ends', async () => {
    const api = fakeAdminApi({ table: vi.fn().mockRejectedValue(new ApiError(401, 'session_expired')) });
    renderAdmin('/admin/sectors', api);

    expect(await screen.findByText('Сесію завершено. Увійдіть знову.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Вхід адміністратора' })).toBeInTheDocument();
  });
});
