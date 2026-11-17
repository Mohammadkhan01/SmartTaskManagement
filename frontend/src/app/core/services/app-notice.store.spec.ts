import { TestBed } from '@angular/core/testing';
import { AppNoticeStore } from './app-notice.store';

describe('AppNoticeStore', () => {
  it('shows and clears the app-wide message', () => {
    const store = TestBed.inject(AppNoticeStore);
    expect(store.message()).toBeNull();

    store.show('Server unreachable');
    expect(store.message()).toBe('Server unreachable');

    store.clear();
    expect(store.message()).toBeNull();
  });
});
