import { DatePipe } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { UtcDatePipe } from './utc-date.pipe';

describe('UtcDatePipe', () => {
  let pipe: UtcDatePipe;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [DatePipe] });
    pipe = TestBed.runInInjectionContext(() => new UtcDatePipe());
  });

  it('treats offset-less API timestamps as UTC', () => {
    expect(pipe.transform('2026-03-04T10:15:00', 'yyyy-MM-dd HH:mm', 'UTC')).toBe('2026-03-04 10:15');
    expect(pipe.transform('2026-03-04T10:15:00.1234567', 'HH:mm', '+0200')).toBe('12:15');
  });

  it('respects explicit offsets and the Z suffix', () => {
    expect(pipe.transform('2026-03-04T10:15:00Z', 'HH:mm', 'UTC')).toBe('10:15');
    expect(pipe.transform('2026-03-04T10:15:00+02:00', 'HH:mm', 'UTC')).toBe('08:15');
    expect(pipe.transform('2026-03-04T10:15:00-0500', 'HH:mm', 'UTC')).toBe('15:15');
  });

  it('uses mediumDate by default', () => {
    expect(pipe.transform('2026-03-04T10:15:00', undefined, 'UTC')).toBe('Mar 4, 2026');
  });

  it('returns null for empty values', () => {
    expect(pipe.transform(null)).toBeNull();
    expect(pipe.transform(undefined)).toBeNull();
    expect(pipe.transform('')).toBeNull();
  });
});
