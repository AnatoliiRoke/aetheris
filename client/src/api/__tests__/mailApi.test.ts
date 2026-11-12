import { describe, expect, it, vi } from 'vitest';
import type { HttpClient } from '../http';
import { createMailApi } from '../mailApi';

describe('mail API responses', () => {
  it('converts the server fields while keeping scan report parameters intact', async () => {
    const parameters = JSON.stringify({ snapshot_id: 1, systems: [], free_fields: [] });
    const get = vi.fn(async (path: string) => {
      if (path.endsWith('/letters/20')) return { letter: { id: 20, kind_code: 'report', system_kind_code: 'scan_report', is_read: true }, body: '', text_key: 'mail.scan_report', text_parameters: parameters };
      if (path.endsWith('/unread')) return { unread_count: 1 };
      return { folders: [{ code: 'incoming', unread_count: 1, total_count: 2 }], categories: [{ id: 3, name_key: 'mail.category.mail_category_3.name' }], letters: [{ id: 20, kind_code: 'report', system_kind_code: 'scan_report', is_read: true, created_at: '2026-11-12T17:38:55Z' }], unread_count: 1, total_count: 2 };
    });
    const api = createMailApi({ get } as unknown as HttpClient);
    const list = await api.mail();
    expect(list.letters[0]).toMatchObject({ id: 20, kindCode: 'report', systemKindCode: 'scan_report', isRead: true });
    expect(list.folders[0]).toMatchObject({ unreadCount: 1, totalCount: 2 });
    expect(list.categories[0]?.nameKey).toBe('mail.category.mail_category_3.name');
    expect(await api.letter(20)).toMatchObject({ textKey: 'mail.scan_report', textParameters: parameters, letter: { systemKindCode: 'scan_report' } });
    expect(await api.unread()).toEqual({ unreadCount: 1 });
  });
});
