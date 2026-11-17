import { DatePipe } from '@angular/common';
import { Pipe, PipeTransform, inject } from '@angular/core';

const HAS_OFFSET = /(Z|[+-]\d{2}:?\d{2})$/i;

/**
 * DatePipe for API `*Utc` fields. EF Core returns DateTime values read from SQL
 * Server without an offset, which DatePipe would otherwise interpret as local time.
 */
@Pipe({ name: 'utcDate', standalone: true })
export class UtcDatePipe implements PipeTransform {
  private readonly datePipe = inject(DatePipe);

  transform(value: string | null | undefined, format = 'mediumDate', timezone?: string): string | null {
    if (!value) return null;
    const normalized = HAS_OFFSET.test(value) ? value : `${value}Z`;
    return this.datePipe.transform(normalized, format, timezone);
  }
}
