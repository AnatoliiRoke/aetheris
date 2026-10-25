import { useContext } from 'react';
import { RouterContext, type RouterValue } from './context';

/**
 * The current route and the navigation function.
 */
export function useRouter(): RouterValue {
  const value = useContext(RouterContext);

  if (!value) {
    throw new Error('useRouter must be used inside RouterProvider.');
  }

  return value;
}
