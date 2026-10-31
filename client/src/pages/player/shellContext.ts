import { createContext } from 'react';
import type { ShellStore } from './shellStore';

/**
 * The shared state of the frame of the screens. The value is made once for a signed-in player
 * and never changes, so reading it wakes nobody: a component wakes only when the part of the
 * state it selected changes (task 00020, section 7.8).
 */
export const ShellContext = createContext<ShellStore | null>(null);
