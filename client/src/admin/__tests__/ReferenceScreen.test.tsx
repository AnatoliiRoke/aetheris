import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/http';
import { adminTexts, renderAdmin } from '../../test/renderAdmin';
import { fakeTextsApi } from '../../test/renderPage';
import type { AdminApi, Page } from '../adminApi';
import type { ReferenceApi, ReferenceKind, ReferenceKinds, ReferenceRecord, ReferenceRow } from '../referenceApi';

const readPermissions = ['admin.me', 'admin.reference_kinds_read', 'admin.world_resources_read', 'admin.world_resource_read', 'admin.world_resource_groups_read'];
const changePermissions = ['admin.world_reference_row_added', 'admin.world_resource_updated', 'admin.world_resource_archived', 'admin.world_resource_restored'];

function kind(name: string, route: string, permissionPrefix: string, added: string, fields: ReferenceKind['fields']): ReferenceKind {
  return {
    kind: name,
    code_group: name,
    name_key_prefix: `world.${name}`,
    title_key: `admin.reference.${name}.title`,
    sort_order: 10,
    route,
    permissions: {
      list_read: `${permissionPrefix}s_read`,
      record_read: `${permissionPrefix}_read`,
      added,
      updated: `${permissionPrefix}_updated`,
      archived: `${permissionPrefix}_archived`,
      restored: `${permissionPrefix}_restored`,
    },
    fields,
  };
}

const field = (overrides: Partial<ReferenceKind['fields'][number]>): ReferenceKind['fields'][number] => ({
  name: 'name',
  type: 'text',
  label_key: 'admin.reference.field.name',
  is_required: false,
  has_languages: false,
  in_list: false,
  is_decimal: false,
  ...overrides,
});

const resourceKind = kind('resource', '/world/resources', 'admin.world_resource', 'admin.world_reference_row_added', [
  field({ name: 'image_key', type: 'image', label_key: 'admin.reference.field.image' }),
  field({ name: 'name', is_required: true, has_languages: true, in_list: true }),
  field({ name: 'description', label_key: 'admin.reference.field.description', is_required: true, has_languages: true }),
  field({ name: 'resource_group_id', type: 'reference', label_key: 'admin.reference.field.resource_group', is_required: true, in_list: true, reference_kind: 'resource_group' }),
]);

const groupKind = kind('resource_group', '/world/resource-groups', 'admin.world_resource_group', 'admin.world_resource_group_added', [
  field({ name: 'name', is_required: true, has_languages: true, in_list: true }),
]);

const described: ReferenceKinds = { kinds: [resourceKind, groupKind], languages: ['uk', 'en'], max_length: 2000, image_types: ['image/png', 'image/webp'], image_max_bytes: 1024 };

const texts = {
  uk: {
    ...adminTexts.uk,
    'admin.reference.resource.title': 'Ресурси',
    'admin.reference.resource_group.title': 'Групи ресурсів',
    'admin.reference.field.name': 'Назва',
    'admin.reference.field.description': 'Опис',
    'admin.reference.field.image': 'Зображення',
    'admin.reference.field.resource_group': 'Група ресурсів',
    'world.resource.resource_2.name': 'Олово',
  },
  en: {},
};

const iron: ReferenceRow = { id: 1, code: 'resource_1', name_key: 'world.resource.resource_1.name', image_key: null, is_archived: false, version: 3, values: { resource_group_id: 1 } };
const tin: ReferenceRow = { id: 2, code: 'resource_2', name_key: 'world.resource.resource_2.name', image_key: null, is_archived: true, version: 5, values: { resource_group_id: 1 } };
const metals: ReferenceRow = { id: 1, code: 'resource_group_1', name_key: 'world.resource_group.resource_group_1.name', image_key: null, is_archived: false, version: 1, values: {} };

const ironRecord: ReferenceRecord = {
  id: 1,
  code: 'resource_1',
  name_key: 'world.resource.resource_1.name',
  description_key: 'world.resource.resource_1.description',
  image_key: null,
  is_archived: false,
  version: 3,
  values: { image_key: null, name: { uk: 'Залізо', en: 'Iron' }, description: { uk: 'Метал', en: 'Metal' }, resource_group_id: 1 },
};

function page(items: ReferenceRow[]): Page<ReferenceRow> {
  return { items, total: items.length, page: 1, page_size: 50 };
}

function fakeReferences(overrides: Partial<ReferenceApi> = {}): ReferenceApi {
  return {
    kinds: vi.fn().mockResolvedValue(described),
    list: vi.fn((target: ReferenceKind, query: { state: string }) => {
      if (target.kind === 'resource_group') {
        return Promise.resolve(page([metals]));
      }

      const rows = [iron, tin];

      return Promise.resolve(page(query.state === 'all' ? rows : rows.filter((row) => row.is_archived === (query.state === 'archived'))));
    }),
    read: vi.fn().mockResolvedValue(ironRecord),
    create: vi.fn().mockResolvedValue({ id: 7, version: 1 }),
    update: vi.fn().mockResolvedValue({ id: 1, version: 4 }),
    setArchived: vi.fn().mockResolvedValue({ id: 1, version: 4 }),
    uploadImage: vi.fn().mockResolvedValue({ image_key: 'a'.repeat(32) + '.png' }),
    imageUrl: (key: string) => `/api/v1/images/${key}`,
    ...overrides,
  };
}

function fakeApi(permissions: string[]): AdminApi {
  return new Proxy({} as AdminApi, {
    get: (_, property) =>
      property === 'me'
        ? vi.fn().mockResolvedValue({
            admin_id: 'admin-1',
            display_name: 'o***@example.test',
            role_codes: ['owner'],
            permission_codes: permissions,
            idle_minutes: 30,
            second_factor: 'none',
            require_totp: false,
          })
        : vi.fn().mockResolvedValue(undefined),
  });
}

function renderReference(path: string, permissions: string[], references: ReferenceApi = fakeReferences()) {
  return renderAdmin(path, fakeApi(permissions), fakeTextsApi(texts), references);
}

describe('admin reference kinds (task 00023)', () => {
  it('builds the menu on the left from the kinds the server describes, and hides the section without the permission', async () => {
    renderReference('/admin/reference', readPermissions);

    const menu = await screen.findByRole('navigation', { name: 'Види довідників' });
    expect(within(menu).getByRole('link', { name: 'Ресурси' })).toBeInTheDocument();
    expect(within(menu).getByRole('link', { name: 'Групи ресурсів' })).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Ресурси' })).toBeInTheDocument();
  });

  it('keeps the section out of the menu of a role without the permission', async () => {
    renderReference('/admin/reference', ['admin.me', 'admin.config_read']);

    const navigation = await screen.findByRole('navigation', { name: 'Розділи адмін-панелі' });
    expect(within(navigation).queryByRole('link', { name: 'Довідники' })).not.toBeInTheDocument();
  });

  it('hides the archived rows of the list until the filter «Архівні» shows them', async () => {
    const references = fakeReferences();
    renderReference('/admin/reference/resource', readPermissions, references);
    const user = userEvent.setup();

    expect(await screen.findByRole('link', { name: 'Залізо' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Олово' })).not.toBeInTheDocument();
    expect(await screen.findAllByRole('cell', { name: 'Видобувні' })).not.toHaveLength(0);

    await user.click(screen.getByRole('checkbox', { name: 'Архівні' }));

    expect(await screen.findByRole('link', { name: 'Олово' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Залізо' })).not.toBeInTheDocument();
    expect(references.list).toHaveBeenLastCalledWith(resourceKind, { page: 1, search: undefined, state: 'archived' });
  });

  it('asks for the name and the description in every language and a reason before it saves', async () => {
    const references = fakeReferences();
    renderReference('/admin/reference/resource/new', [...readPermissions, ...changePermissions], references);
    const user = userEvent.setup();

    const form = await screen.findByRole('form', { name: 'Новий запис: Ресурси' });
    const save = within(form).getByRole('button', { name: 'Зберегти' });

    await user.type(within(form).getByRole('textbox', { name: 'Назва (uk)' }), 'Свинець');
    await user.type(within(form).getByRole('textbox', { name: 'Назва (en)' }), 'Lead');
    await user.type(within(form).getByRole('textbox', { name: 'Опис (uk)' }), 'Метал');
    await user.type(within(form).getByRole('textbox', { name: /^Причина/ }), 'Новий ресурс');
    expect(save).toBeDisabled();

    await user.type(within(form).getByRole('textbox', { name: 'Опис (en)' }), 'Metal');
    await user.selectOptions(await within(form).findByRole('combobox', { name: 'Група ресурсів' }), '1');
    expect(save).toBeEnabled();

    await user.click(save);

    await waitFor(() =>
      expect(references.create).toHaveBeenCalledWith(
        resourceKind,
        { image_key: null, name: { uk: 'Свинець', en: 'Lead' }, description: { uk: 'Метал', en: 'Metal' }, resource_group_id: 1 },
        'Новий ресурс',
      ),
    );
  });

  it('shows the placeholder without a picture and refuses a file of another type or a too large one', async () => {
    const references = fakeReferences();
    renderReference('/admin/reference/resource/1', [...readPermissions, ...changePermissions], references);
    const user = userEvent.setup({ applyAccept: false });

    const form = await screen.findByRole('form', { name: 'Залізо' });
    expect(within(form).getByRole('img', { name: 'Зображення немає' })).toBeInTheDocument();

    const input = within(form).getByLabelText(/^Зображення/, { selector: 'input' });
    await user.upload(input, new File(['GIF89a'], 'picture.gif', { type: 'image/gif' }));
    expect(await within(form).findByText('Дозволені лише зображення PNG або WebP.')).toBeInTheDocument();

    await user.upload(input, new File([new Uint8Array(2048)], 'picture.png', { type: 'image/png' }));
    expect(await within(form).findByText('Файл завеликий.')).toBeInTheDocument();
    expect(references.uploadImage).not.toHaveBeenCalled();

    await user.upload(input, new File([new Uint8Array(16)], 'picture.png', { type: 'image/png' }));
    expect(await within(form).findByRole('img', { name: 'Зображення' })).toBeInTheDocument();
  });

  it('says that the data changed on 409 and shows a refused field next to it on 422', async () => {
    const update = vi
      .fn()
      .mockRejectedValueOnce(new ApiError(409, 'reference_version_conflict'))
      .mockRejectedValueOnce(new ApiError(422, 'validation_failed', { resource_group_id: ['archived'] }));
    renderReference('/admin/reference/resource/1', [...readPermissions, ...changePermissions], fakeReferences({ update }));
    const user = userEvent.setup();

    const form = await screen.findByRole('form', { name: 'Залізо' });
    await user.type(within(form).getByRole('textbox', { name: /^Причина/ }), 'Правка');
    await user.click(within(form).getByRole('button', { name: 'Зберегти' }));

    expect(await within(form).findByRole('button', { name: 'Оновити дані' })).toBeInTheDocument();

    await user.click(within(form).getByRole('button', { name: 'Зберегти' }));

    expect(await within(form).findByText('Запис в архіві: його не можна вибрати для нових записів.')).toBeInTheDocument();
  });

  it('opens the form for reading only and hides the creation and the archive without the permissions of the change', async () => {
    renderReference('/admin/reference/resource', readPermissions);

    expect(await screen.findByRole('link', { name: 'Залізо' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Створити' })).not.toBeInTheDocument();

    await userEvent.setup().click(screen.getByRole('link', { name: 'Залізо' }));

    const form = await screen.findByRole('form', { name: 'Залізо' });
    expect(within(form).getByText('Запис відкрито лише для читання: вашій ролі не надано права змінювати цей довідник.')).toBeInTheDocument();
    expect(within(form).getByRole('textbox', { name: 'Назва (uk)' })).toBeDisabled();
    expect(within(form).queryByRole('button', { name: 'Зберегти' })).not.toBeInTheDocument();
    expect(within(form).queryByRole('button', { name: 'Архівувати' })).not.toBeInTheDocument();
  });
});
