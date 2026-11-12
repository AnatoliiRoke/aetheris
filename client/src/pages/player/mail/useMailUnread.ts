import { useCallback } from 'react';
import { useShellActions, useShellSelector } from '../useShell';

/**
 * The number of the letters that are not read: the counter of the mail in the header and in the
 * menu (task 00060).
 *
 * The counter lives in the shared state of the frame, so the header and the screen of the mail
 * read one answer and the server is asked once. It is read when the frame loads and again after
 * every command of the mail, so it is never a made-up number. The live counter of the header (the
 * event of the server) belongs to the task of the events of the player.
 */

/**
 * Reads the number of the letters that are not read and keeps it for the header.
 */
export function useMailUnread() {
  const actions = useShellActions();
  const count = useShellSelector((state) => state.mailUnread);
  const loaded = useShellSelector((state) => state.mailUnreadLoaded);
  const reload = useCallback(() => actions.loadMailUnread(true), [actions]);

  return { count: loaded ? count : 0, reload };
}
