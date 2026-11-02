import { render } from '@testing-library/react';
import { StrictMode } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { pageRequestFinished, pageRequestStarted } from '../pageMark';
import { PageTrace } from '../PageTrace';

describe('PageTrace', () => {
  it('writes one line per transition, also under the strict mode, and draws nothing', () => {
    const debug = vi.spyOn(console, 'debug').mockImplementation(() => undefined);
    const view = render(
      <StrictMode>
        <PageTrace path="/game" />
      </StrictMode>,
    );

    expect(view.container).toBeEmptyDOMElement();
    expect(debug).toHaveBeenCalledTimes(1);
    expect(String(debug.mock.calls[0][0])).toContain('page /game ready');

    // The same path again (a redraw of the page) is not a transition.
    view.rerender(
      <StrictMode>
        <PageTrace path="/game" />
      </StrictMode>,
    );
    expect(debug).toHaveBeenCalledTimes(1);

    view.rerender(
      <StrictMode>
        <PageTrace path="/planet" />
      </StrictMode>,
    );
    expect(debug).toHaveBeenCalledTimes(2);
    expect(String(debug.mock.calls[1][0])).toContain('page /planet ready');
    expect(pageRequestStarted()).toBeNull();
    pageRequestFinished(null);
  });
});
