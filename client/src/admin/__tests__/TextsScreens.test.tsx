import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/http';
import { renderAdmin } from '../../test/renderAdmin';
import type { AdminApi, Page, Row, WorldReference } from '../adminApi';

const permissions = ['admin.me', 'admin.texts_read', 'admin.text_updated', 'admin.world_reference_read', 'admin.world_reference_row_added'];

const reference: WorldReference = {
  resource_groups: [
    { id: 1, code: 'resource_group_1', name_key: 'world.resource_group.resource_group_1.name', group_code: null },
    { id: 2, code: 'resource_group_2', name_key: 'world.resource_group.resource_group_2.name', group_code: null },
  ],
  resources: [{ id: 1, code: 'resource_1', name_key: 'world.resource.resource_1.name', description_key: 'world.resource.resource_1.description', group_code: null, group_id: 1 }],
  object_types: [],
  size_classes: [],
};

const textRows: Row[] = [
  { key: 'world.resource.resource_1.name', language: 'uk', text: 'Залізо', version: 3, updated_by: 'seed', updated_at: '2026-10-28T10:00:00Z' },
  { key: 'world.resource.resource_1.name', language: 'en', text: null, version: 0, updated_by: null, updated_at: null },
];

function page(items: Row[]): Page<Row> {
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
    table: vi.fn().mockResolvedValue(page(textRows)),
    configHistory: vi.fn(),
    changeConfig: vi.fn(),
    rollbackConfig: vi.fn(),
    clock: vi.fn(),
    reference: vi.fn().mockResolvedValue(reference),
    estimate: vi.fn(),
    startGeneration: vi.fn(),
    run: vi.fn(),
    fieldCard: vi.fn(),
    updateField: vi.fn(),
    bodyCard: vi.fn(),
    updateBody: vi.fn(),
    systemCard: vi.fn(),
    updateSystem: vi.fn(),
    updateObjectType: vi.fn(),
    updateRule: vi.fn(),
    addRule: vi.fn(),
    updateRuleGroup: vi.fn(),
    textSettings: vi.fn().mockResolvedValue({ languages: ['uk', 'en'], max_length: 2000 }),
    changeText: vi.fn().mockResolvedValue(undefined),
    createResource: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  };
}

describe('admin texts and resources (task 00022)', () => {
  it('lists every key in every language, the missing translation included, and filters by language and gaps', async () => {
    const api = fakeApi();
    renderAdmin('/admin/texts', api);

    expect(await screen.findByRole('heading', { level: 1, name: 'Тексти ігрових даних' })).toBeInTheDocument();
    expect(await screen.findByText('Залізо')).toBeInTheDocument();
    expect(screen.getByText('немає перекладу')).toBeInTheDocument();

    const user = userEvent.setup();
    await user.selectOptions(screen.getByRole('combobox', { name: 'Лише без перекладу' }), 'true');

    await waitFor(() =>
      expect(api.table).toHaveBeenLastCalledWith('texts', expect.objectContaining({ filters: expect.objectContaining({ missing: 'true' }) })),
    );
  });

  it('changes a text only with a reason, reloads the texts of the site and reports a conflict', async () => {
    const changeText = vi.fn().mockResolvedValueOnce(undefined).mockRejectedValueOnce(new ApiError(409, 'text_version_conflict'));
    const api = fakeApi({ changeText });
    const { texts } = renderAdmin('/admin/texts', api);
    const user = userEvent.setup();

    await user.click(await screen.findByRole('row', { name: 'Змінити текст world.resource.resource_1.name, мова uk' }));

    const dialog = await screen.findByRole('dialog');
    const field = within(dialog).getByRole('textbox', { name: 'Текст' });
    await user.clear(field);
    await user.type(field, 'Ферум');
    await user.click(within(dialog).getByRole('button', { name: 'Зберегти' }));

    expect(changeText).not.toHaveBeenCalled();

    await user.type(within(dialog).getByRole('textbox', { name: /^Причина/ }), 'Нова назва');
    await user.click(within(dialog).getByRole('button', { name: 'Зберегти' }));

    await waitFor(() => expect(changeText).toHaveBeenCalledWith('world.resource.resource_1.name', 'uk', 'Ферум', 3, 'Нова назва'));
    expect(await within(dialog).findByText('Текст збережено. Гравці побачать його після оновлення сторінки.')).toBeInTheDocument();
    await waitFor(() => expect(vi.mocked(texts.pack).mock.calls.length).toBeGreaterThan(1));

    await user.click(within(dialog).getByRole('button', { name: 'Скасувати' }));
    await user.click(await screen.findByRole('row', { name: 'Змінити текст world.resource.resource_1.name, мова uk' }));
    const again = await screen.findByRole('dialog');
    await user.type(within(again).getByRole('textbox', { name: /^Причина/ }), 'Ще раз');
    await user.click(within(again).getByRole('button', { name: 'Зберегти' }));

    expect(await within(again).findByRole('button', { name: 'Оновити дані' })).toBeInTheDocument();
  });

  it('creates a resource with its name and description in every language; the server gives the code', async () => {
    const createResource = vi
      .fn()
      .mockRejectedValueOnce(new ApiError(400, 'validation_failed', { 'description.en': ['required'] }))
      .mockResolvedValueOnce(undefined);
    const api = fakeApi({ createResource });
    renderAdmin('/admin/resources', api);
    const user = userEvent.setup();

    expect(await screen.findByRole('cell', { name: 'Залізо' })).toBeInTheDocument();
    expect(screen.getByRole('cell', { name: 'Видобувні' })).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Новий ресурс' }));
    const form = await screen.findByRole('form', { name: 'Новий ресурс' });

    await user.selectOptions(within(form).getByRole('combobox', { name: 'Група' }), '2');
    await user.type(await within(form).findByRole('textbox', { name: 'Назва (uk)' }), 'Олово');
    await user.type(within(form).getByRole('textbox', { name: 'Назва (en)' }), 'Tin');
    await user.type(within(form).getByRole('textbox', { name: 'Опис (uk)' }), 'Метал для сплавів.');
    await user.type(within(form).getByRole('textbox', { name: /^Причина/ }), 'Новий ресурс');
    await user.click(within(form).getByRole('button', { name: 'Зберегти' }));

    expect(await within(form).findByText('Обовʼязкове поле.', { exact: false })).toBeInTheDocument();

    await user.type(within(form).getByRole('textbox', { name: /Опис \(en\)/ }), 'A metal for alloys.');
    await user.click(within(form).getByRole('button', { name: 'Зберегти' }));

    await waitFor(() =>
      expect(createResource).toHaveBeenLastCalledWith({
        resource_group_id: 2,
        name: { uk: 'Олово', en: 'Tin' },
        description: { uk: 'Метал для сплавів.', en: 'A metal for alloys.' },
        reason: 'Новий ресурс',
      }),
    );
    expect(await screen.findByText('Ресурс додано.')).toBeInTheDocument();
  });
});
