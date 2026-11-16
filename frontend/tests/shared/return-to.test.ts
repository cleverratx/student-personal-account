import { safeReturnTo } from '@/shared/lib';

describe('safeReturnTo', () => {
  it.each([
    ['/requests', '/requests'],
    ['/requests?tab=done#top', '/requests?tab=done#top'],
    ['//evil.example', undefined],
    ['/\\evil.example', undefined],
    ['https://evil.example', undefined],
    ['javascript:alert(1)', undefined],
    ['/login', undefined],
    ['/login?returnTo=/today', undefined],
    ['', undefined],
    [null, undefined],
  ])('%s → %s', (value, expected) => {
    expect(safeReturnTo(value)).toBe(expected);
  });
});
