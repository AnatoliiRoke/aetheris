import { useEffect, useState } from 'react';

/**
 * The state of a request made by a section.
 */
export type AsyncState<T> = { status: 'loading' } | { status: 'error' } | { status: 'ready'; data: T };

/**
 * Runs a request and keeps only the answer of the latest one.
 *
 * @param load The request; a new function (from `useCallback`) starts a new request.
 */
export function useAsync<T>(load: () => Promise<T>): AsyncState<T> {
  const [settled, setSettled] = useState<{ load: () => Promise<T>; state: AsyncState<T> } | null>(null);

  useEffect(() => {
    let current = true;

    load().then(
      (data) => current && setSettled({ load, state: { status: 'ready', data } }),
      () => current && setSettled({ load, state: { status: 'error' } }),
    );

    return () => {
      current = false;
    };
  }, [load]);

  // An answer to an older request does not count: the section shows the loading state.
  return settled?.load === load ? settled.state : { status: 'loading' };
}
