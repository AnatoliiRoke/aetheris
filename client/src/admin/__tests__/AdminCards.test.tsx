import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/http';
import type { AdminApi, FieldCard, Page, Row, WorldReference } from '../adminApi';
import { renderAdmin } from '../../test/renderAdmin';

const permissions = [
  'admin.me',
  'admin.world_fields_read',
  'admin.world_field_read',
  'admin.world_field_updated',
  'admin.world_objects_read',
  'admin.world_reference_read',
  'admin.world_generation_rules_read',
  'admin.config_read',
];

const reference: WorldReference = {
  resources: [
    { id: 1, code: 'resource_1', name_key: 'world.resource.resource_1.name', group_code: null, group_id: 1 },
    { id: 5, code: 'resource_5', name_key: 'world.resource.resource_5.name', group_code: null, group_id: 2 },
  ],
  object_types: [
    { id: 11, code: 'object_type_11', name_key: 'world.object_type.object_type_11.name', group_code: 'asteroid_field' },
    { id: 5, code: 'object_type_5', name_key: 'world.object_type.object_type_5.name', group_code: 'planet' },
  ],
  resource_groups: [
    { id: 1, code: 'resource_group_1', name_key: 'world.resource_group.resource_group_1.name', group_code: null },
    { id: 2, code: 'resource_group_2', name_key: 'world.resource_group.resource_group_2.name', group_code: null },
  ],
  size_classes: [{ id: 1, code: 'size_class_1', name_key: 'world.size_class.size_class_1.name', group_code: null }],
};

const fieldRow = {
  id: 3,
  celestial_object_id: 30,
  name: 'S1-F1',
  object_type_id: 11,
  object_type_code: 'object_type_11',
  object_type_name_key: 'world.object_type.object_type_11.name',
  sector_id: 1,
  system_id: null,
  system_name: null,
  x: 12.5,
  y: 40,
  z: 0,
  initial_mass: 900000,
  mass: 800000,
  depleted_at: null,
  composition_ids: [
    { resource_id: 1, resource_code: 'resource_1', resource_name_key: 'world.resource.resource_1.name', coefficient: 0.296 },
    { resource_id: 5, resource_code: 'resource_5', resource_name_key: 'world.resource.resource_5.name', coefficient: 0.04 },
  ],
};

const fieldCard: FieldCard = { ...fieldRow, version: 4, bounds: { max_composition_share: 0.95, sector_size: 1000, sector_depth: 0 } };

function page<T>(items: T[]): Page<T> {
  return { items, total: items.length, page: 1, page_size: 50 };
}

function fakeApi(overrides: Partial<AdminApi> = {}): AdminApi {
  return {
    login: vi.fn(),
    logout: vi.fn(),
    me: vi.fn().mockResolvedValue({
      admin_id: 'admin-1',
      display_name: 'o***@example.test',
      role_codes: ['owner'],
      permission_codes: permissions,
      idle_minutes: 30,
      second_factor: 'none',
      require_totp: false,
    }),
    table: vi.fn().mockResolvedValue(page<Row>([fieldRow as unknown as Row])),
    configHistory: vi.fn().mockResolvedValue(page([])),
    changeConfig: vi.fn(),
    rollbackConfig: vi.fn(),
    clock: vi.fn().mockResolvedValue({ multiplier: 1, max_multiplier: 100, read_only: true }),
    reference: vi.fn().mockResolvedValue(reference),
    estimate: vi.fn(),
    startGeneration: vi.fn(),
    run: vi.fn(),
    fieldCard: vi.fn().mockResolvedValue(fieldCard),
    updateField: vi.fn().mockResolvedValue({ ...fieldCard, version: 5 }),
    bodyCard: vi.fn(),
    updateBody: vi.fn(),
    systemCard: vi.fn(),
    updateSystem: vi.fn(),
    updateObjectType: vi.fn(),
    updateRule: vi.fn(),
    addRule: vi.fn(),
    updateRuleGroup: vi.fn(),
    textSettings: vi.fn().mockResolvedValue({ languages: ['uk', 'en'], max_length: 2000 }),
    changeText: vi.fn(),
    createResource: vi.fn(),
    ...overrides,
  };
}

describe('admin cards (task 00009)', () => {
  it('shows names, shares in per cent with the empty rock and the coordinates of a field', async () => {
    renderAdmin('/admin/fields', fakeApi());

    const row = (await screen.findByText('S1-F1')).closest('tr')!;

    expect(within(row).getByText('Звичайне поле')).toHaveAttribute('title', 'object_type_11');
    expect(within(row).getByText('Кварцит 4%')).toHaveAttribute('title', 'resource_5');
    expect(within(row).getByText('Залізо 29,6%')).toBeInTheDocument();
    expect(within(row).getByText('порожня порода 66,4%')).toBeInTheDocument();
    expect(within(row).getByText('12,5')).toBeInTheDocument();
    expect(within(row).queryByText(/quartzite|field_ordinary/)).not.toBeInTheDocument();
  });

  it('shows a deposit as a richness multiplier', async () => {
    const body = {
      id: 9,
      name: 'S1-1-3',
      object_class: 'planet',
      object_type_code: 'object_type_5',
      object_type_name_key: 'world.object_type.object_type_5.name',
      size_class_code: 'size_class_1',
      size_class_name_key: 'world.size_class.size_class_1.name',
      x: 1,
      y: 2,
      z: 0,
      deposit_ids: [{ resource_id: 1, resource_code: 'resource_1', resource_name_key: 'world.resource.resource_1.name', coefficient: 0.949 }],
    };
    renderAdmin('/admin/objects', fakeApi({ table: vi.fn().mockResolvedValue(page<Row>([body])) }));

    expect(await screen.findByText('Залізо ×0,949')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Багатство родовища (множник)' })).toHaveAttribute(
      'title',
      '1,00 = стандартне родовище типу; 0,5–1,5 діапазон генерації.',
    );
    const row = screen.getByText('S1-1-3').closest('tr')!;
    expect(within(row).getByText('Землеподібна')).toHaveAttribute('title', 'object_type_5');
  });

  it('opens the card of a field and saves it only with a reason', async () => {
    const user = userEvent.setup();
    const api = fakeApi();
    renderAdmin('/admin/fields', api);

    await user.click(await screen.findByText('S1-F1'));
    const dialog = await screen.findByRole('dialog', { name: 'Поле «S1-F1»' });
    expect(api.fieldCard).toHaveBeenCalledWith(3);

    const mass = within(dialog).getByDisplayValue('800000');
    await user.clear(mass);
    await user.type(mass, '700000');
    await user.click(within(dialog).getByRole('button', { name: 'Зберегти' }));

    expect(await within(dialog).findByText('Обовʼязкове поле.')).toBeInTheDocument();
    expect(api.updateField).not.toHaveBeenCalled();

    await user.type(within(dialog).getByPlaceholderText('Чому змінюєте значення'), 'Fix mass');
    await user.click(within(dialog).getByRole('button', { name: 'Зберегти' }));

    await waitFor(() =>
      expect(api.updateField).toHaveBeenCalledWith(
        3,
        expect.objectContaining({
          version: 4,
          reason: 'Fix mass',
          mass: 700000,
          composition_ids: [
            { resource_id: 1, coefficient: 0.296 },
            { resource_id: 5, coefficient: 0.04 },
          ],
        }),
      ),
    );
    expect(await within(dialog).findByText('Збережено, запис є в журналі.')).toBeInTheDocument();
  });

  it('tells that the data changed when the version is old', async () => {
    const user = userEvent.setup();
    const api = fakeApi({ updateField: vi.fn().mockRejectedValue(new ApiError(409, 'world_version_conflict')) });
    renderAdmin('/admin/fields', api);

    await user.click(await screen.findByText('S1-F1'));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByPlaceholderText('Чому змінюєте значення'), 'Late');
    await user.click(within(dialog).getByRole('button', { name: 'Зберегти' }));

    expect(await within(dialog).findByText(/Дані змінилися/)).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Оновити дані' }));
    await waitFor(() => expect(api.fieldCard).toHaveBeenCalledTimes(2));
  });

  it('shows the errors of the server under the inputs', async () => {
    const user = userEvent.setup();
    const api = fakeApi({
      updateField: vi.fn().mockRejectedValue(new ApiError(400, 'validation_failed', { composition_ids: ['share_too_large'] })),
    });
    renderAdmin('/admin/fields', api);

    await user.click(await screen.findByText('S1-F1'));
    const dialog = await screen.findByRole('dialog');
    await user.type(within(dialog).getByPlaceholderText('Чому змінюєте значення'), 'Too much');
    await user.click(within(dialog).getByRole('button', { name: 'Зберегти' }));

    expect(await within(dialog).findByText('Сума часток більша за дозволену межу.')).toBeInTheDocument();
  });

  it('shows the Ukrainian description of a parameter', async () => {
    const parameter = {
      key: 'world.system.field_probability',
      value: '0.15',
      type: 'decimal',
      minimum: '0',
      maximum: '1',
      default_value: '0.15',
      description: 'Probability of a field on the outermost orbit of a new system.',
      read_only: false,
      version: 1,
      updated_by: 'seed',
      updated_at: '2026-10-26T10:00:00Z',
    };
    renderAdmin('/admin/config', fakeApi({ table: vi.fn().mockResolvedValue(page<Row>([parameter])) }));

    expect(await screen.findByText('Імовірність поля на найдальшій орбіті нової системи.')).toBeInTheDocument();
    expect(screen.queryByText('Probability of a field on the outermost orbit of a new system.')).not.toBeInTheDocument();
  });
});
