import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { IconComponent } from './icon.component';

/** The paging fields shared by every paged list response from the API. */
export interface PageInfo {
  pageNumber: number;
  totalPages: number;
  totalCount: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

@Component({
  selector: 'app-pagination',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [IconComponent],
  template: `
    <nav class="pagination" aria-label="Pagination">
      <p class="pagination-summary"><strong>{{ page().totalCount }}</strong> {{ countLabel() }}</p>
      <div class="pagination-controls">
        <span class="pagination-page">Page {{ page().pageNumber }} of {{ page().totalPages || 1 }}</span>
        <button type="button" class="btn btn-secondary btn-sm" [disabled]="!page().hasPreviousPage"
          (click)="pageChange.emit(page().pageNumber - 1)">
          <app-icon name="chevronLeft" [size]="16" />
          Previous
        </button>
        <button type="button" class="btn btn-secondary btn-sm" [disabled]="!page().hasNextPage"
          (click)="pageChange.emit(page().pageNumber + 1)">
          Next
          <app-icon name="chevronRight" [size]="16" />
        </button>
      </div>
    </nav>
  `
})
export class PaginationComponent {
  readonly page = input.required<PageInfo>();
  /** Singular noun for the listed items, e.g. "project". */
  readonly noun = input.required<string>();
  readonly pluralNoun = input<string>();

  readonly pageChange = output<number>();

  readonly countLabel = computed(() =>
    this.page().totalCount === 1 ? this.noun() : this.pluralNoun() ?? `${this.noun()}s`);
}
