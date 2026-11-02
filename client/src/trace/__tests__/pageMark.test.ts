import { afterEach, beforeEach, describe, expect, it, vi, type MockInstance } from 'vitest';
import { beginPageTrace, markNavigationStart, pageRequestFinished, pageRequestStarted, settlePageTrace } from '../pageMark';

describe('page ready mark', () => {
  let debug: MockInstance<typeof console.debug>;

  beforeEach(() => {
    debug = vi.spyOn(console, 'debug').mockImplementation(() => undefined);
  });

  afterEach(() => {
    // Closes a mark a test left open, so the next test starts clean.
    beginPageTrace('/clean-up');
    settlePageTrace();
  });

  it('writes one line when the requests of the page are done, with their number', () => {
    markNavigationStart();
    beginPageTrace('/planet/build');
    const first = pageRequestStarted();
    const second = pageRequestStarted();
    settlePageTrace();
    expect(debug).not.toHaveBeenCalled();

    pageRequestFinished(first);
    expect(debug).not.toHaveBeenCalled();

    pageRequestFinished(second);
    expect(debug).toHaveBeenCalledTimes(1);
    expect(String(debug.mock.calls[0][0])).toMatch(/^\[trace\] page \/planet\/build ready in \d+ ms \(2 requests\)$/);

    // The page is already counted: a later request and a second settling write nothing more.
    pageRequestFinished(pageRequestStarted());
    settlePageTrace();
    expect(debug).toHaveBeenCalledTimes(1);
  });

  it('writes one line for a page that asked for nothing', () => {
    beginPageTrace('/game');
    settlePageTrace();
    settlePageTrace();

    expect(debug).toHaveBeenCalledTimes(1);
    expect(String(debug.mock.calls[0][0])).toContain('page /game ready');
    expect(String(debug.mock.calls[0][0])).toContain('(0 requests)');
  });

  it('keeps the running mark when the same path begins again (the strict mode)', () => {
    beginPageTrace('/planet');
    const request = pageRequestStarted();
    beginPageTrace('/planet');
    settlePageTrace();
    pageRequestFinished(request);

    expect(debug).toHaveBeenCalledTimes(1);
    expect(String(debug.mock.calls[0][0])).toContain('(1 requests)');
  });

  it('counts every transition once and ignores the requests of an abandoned page', () => {
    beginPageTrace('/planet');
    const abandoned = pageRequestStarted();
    beginPageTrace('/game');
    const current = pageRequestStarted();

    pageRequestFinished(abandoned);
    expect(debug).not.toHaveBeenCalled();

    pageRequestFinished(current);
    expect(debug).toHaveBeenCalledTimes(1);
    expect(String(debug.mock.calls[0][0])).toContain('page /game ready');
  });

  it('counts the requests that start outside any mark for no page', () => {
    const owner = pageRequestStarted();
    pageRequestFinished(owner);

    expect(owner).toBeNull();
    expect(debug).not.toHaveBeenCalled();
  });
});
