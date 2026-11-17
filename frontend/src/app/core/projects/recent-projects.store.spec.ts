import { RecentProjectsStore } from './recent-projects.store';

describe('RecentProjectsStore', () => {
  let store: RecentProjectsStore;

  beforeEach(() => (store = new RecentProjectsStore()));

  it('keeps the most recently opened projects first, without duplicates, up to four', () => {
    for (const id of ['a', 'b', 'c', 'd', 'e']) {
      store.remember({ id, name: `Project ${id}` });
    }
    store.remember({ id: 'c', name: 'Renamed c' });

    expect(store.projects().map(p => p.id)).toEqual(['c', 'e', 'd', 'b']);
    expect(store.nameOf('c')).toBe('Renamed c');
  });

  it('forgets a deleted project and clears everything on sign-out', () => {
    store.remember({ id: 'a', name: 'A' });
    store.remember({ id: 'b', name: 'B' });

    store.forget('a');
    expect(store.nameOf('a')).toBeUndefined();

    store.clear();
    expect(store.projects()).toEqual([]);
  });
});
