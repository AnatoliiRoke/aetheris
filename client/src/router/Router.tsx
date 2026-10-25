import { useCallback, useEffect, useMemo, useState, type AnchorHTMLAttributes, type MouseEvent, type ReactNode } from 'react';
import { RouterContext, type RouterValue } from './context';
import { useRouter } from './useRouter';

interface Location {
  path: string;
  search: string;
}

function currentLocation(): Location {
  return { path: window.location.pathname, search: window.location.search };
}

/**
 * A small history router: the public site has a handful of flat routes, so a library is
 * not needed. Hash links (`#about`) stay ordinary anchors.
 */
export function RouterProvider({ children }: { children: ReactNode }) {
  const [location, setLocation] = useState<Location>(currentLocation);

  useEffect(() => {
    const onPopState = () => setLocation(currentLocation());
    window.addEventListener('popstate', onPopState);
    return () => window.removeEventListener('popstate', onPopState);
  }, []);

  const navigate = useCallback((to: string, options?: { replace?: boolean }) => {
    if (options?.replace) {
      window.history.replaceState(null, '', to);
    } else {
      window.history.pushState(null, '', to);
    }

    setLocation(currentLocation());

    // A new page starts at its top, unless the link points to a section.
    if (!to.includes('#')) {
      window.scrollTo({ top: 0 });
    }
  }, []);

  const value = useMemo<RouterValue>(
    () => ({ path: location.path, query: new URLSearchParams(location.search), navigate }),
    [location, navigate],
  );

  return <RouterContext.Provider value={value}>{children}</RouterContext.Provider>;
}

type LinkProps = AnchorHTMLAttributes<HTMLAnchorElement> & { to: string };

/**
 * A link inside the site: navigates without reloading the page, but stays a real `<a>`
 * with an `href`, so it works for search engines, new tabs and keyboard users.
 */
export function Link({ to, onClick, ...rest }: LinkProps) {
  const { navigate } = useRouter();

  const handleClick = (event: MouseEvent<HTMLAnchorElement>) => {
    onClick?.(event);

    const isPlainClick = event.button === 0 && !event.metaKey && !event.ctrlKey && !event.shiftKey && !event.altKey;

    if (!event.defaultPrevented && isPlainClick) {
      event.preventDefault();
      navigate(to);
    }
  };

  return <a href={to} onClick={handleClick} {...rest} />;
}

