import { useEffect } from 'react';

/**
 * Sets the title of the browser tab and, when given, the page description.
 */
export function useDocumentTitle(title: string, description?: string): void {
  useEffect(() => {
    document.title = title;

    if (description) {
      document.querySelector('meta[name="description"]')?.setAttribute('content', description);
    }
  }, [title, description]);
}
