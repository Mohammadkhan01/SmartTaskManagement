import { DatePipe } from '@angular/common';
import { UtcDatePipe } from '../../shared/pipes/utc-date.pipe';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, Injector, afterNextRender, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { SessionStore } from '../../core/auth/session-store';
import {
  Project,
  ProjectListQuery,
  ProjectListResponse,
  ProjectStatus
} from '../../core/projects/project.models';
import { ProjectService } from '../../core/projects/project.service';
import { AvatarComponent } from '../../shared/ui/avatar.component';
import { IconComponent } from '../../shared/ui/icon.component';
import { PaginationComponent } from '../../shared/ui/pagination.component';

@Component({
  selector: 'app-projects',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [DatePipe],
  imports: [UtcDatePipe, ReactiveFormsModule, AvatarComponent, IconComponent, PaginationComponent],
  template: `
    <main class="page">
      <header class="page-header">
        <div>
          <h1>Projects</h1>
          <p class="page-subtitle">View and manage the projects you belong to.</p>
        </div>
        @if (canCreate()) {
          <button type="button" class="btn" [class.btn-primary]="!showCreateForm()" [class.btn-secondary]="showCreateForm()"
            [attr.aria-expanded]="showCreateForm()" [attr.aria-controls]="showCreateForm() ? 'create-project-panel' : null"
            (click)="toggleCreateForm()">
            @if (!showCreateForm()) { <app-icon name="plus" [size]="16" /> }
            {{ showCreateForm() ? 'Cancel' : 'Create project' }}
          </button>
        }
      </header>

      @if (showCreateForm()) {
        <section id="create-project-panel" class="panel create-panel" aria-labelledby="create-project-title">
          <div class="create-intro">
            <span class="create-icon"><app-icon name="folder" /></span>
            <div>
              <h2 id="create-project-title">Create a project</h2>
              <p class="panel-meta">Start with a name. Members and tasks can be added once the project exists.</p>
            </div>
          </div>
          <form class="form" [formGroup]="createForm" (ngSubmit)="createProject()">
            <div class="field">
              <label for="project-name">Project name</label>
              <input id="project-name" type="text" formControlName="name" maxlength="150" autocomplete="off"
                placeholder="e.g. Website relaunch"
                [attr.aria-invalid]="createForm.controls.name.touched && createForm.controls.name.invalid"
                [attr.aria-describedby]="createForm.controls.name.touched && createForm.controls.name.invalid ? 'project-name-error' : null">
              @if (createForm.controls.name.touched && createForm.controls.name.invalid) {
                <p id="project-name-error" class="field-error">Enter a project name (maximum 150 characters).</p>
              }
            </div>

            <div class="field">
              <label for="project-description">Description <span class="optional">Optional</span></label>
              <textarea id="project-description" rows="3"
                formControlName="description" maxlength="2000"></textarea>
            </div>

            @if (createError()) {
              <p class="alert error" role="alert">{{ createError() }}</p>
            }

            <div class="form-actions end">
              <button type="button" class="btn btn-secondary" (click)="toggleCreateForm()">
                Cancel
              </button>
              <button type="submit" class="btn btn-primary" [disabled]="creating() || createForm.invalid">
                {{ creating() ? 'Creating…' : 'Create project' }}
              </button>
            </div>
          </form>
        </section>
      }

      <section class="panel data-panel" aria-label="Project list">
        <div class="filters toolbar">
          <label class="search-field">
            <span class="sr-only">Search projects</span>
            <app-icon name="search" [size]="16" />
            <input type="search" placeholder="Search by name or description"
              [value]="searchText()" (input)="onSearch($event)">
          </label>
          <label>
            <span>Status</span>
            <select [value]="statusFilter()" (change)="onStatusChange($event)">
              <option value="">All statuses</option>
              <option value="1">Planning</option>
              <option value="2">Active</option>
              <option value="3">Completed</option>
              <option value="4">On hold</option>
              <option value="5">Archived</option>
            </select>
          </label>
          <label>
            <span>Sort by</span>
            <select [value]="sortBy()" (change)="onSortChange($event)">
              <option value="createdAt">Date created</option>
              <option value="updatedAt">Date updated</option>
              <option value="name">Name</option>
              <option value="status">Status</option>
            </select>
          </label>
        </div>

        @if (loading()) {
          <div class="loading-rows" role="status">
            <span class="sr-only">Loading projects…</span>
            @for (row of skeletonRows; track row) { <span class="skeleton" aria-hidden="true"></span> }
          </div>
        } @else if (error()) {
          <div class="empty-state">
            <span class="empty-state-icon tone-danger"><app-icon name="alert" /></span>
            <p class="error" role="alert">{{ error() }}</p>
            <button type="button" class="btn btn-secondary" (click)="loadProjects()">
              <app-icon name="refresh" [size]="16" />
              Try again
            </button>
          </div>
        } @else if (result(); as data) {
          @if (data.items.length === 0) {
            <div class="empty-state">
              <span class="empty-state-icon"><app-icon name="folder" /></span>
              <h2>No projects found</h2>
              <p>Try changing your search or status filter.</p>
            </div>
          } @else {
            <div class="table-wrap">
              <table class="table table-responsive">
                <thead>
                  <tr>
                    <th scope="col">Project</th>
                    <th scope="col">Status</th>
                    <th scope="col">Created</th>
                    <th scope="col" class="col-actions"><span class="sr-only">Action</span></th>
                  </tr>
                </thead>
                <tbody>
                  @for (project of data.items; track project.id) {
                    <tr>
                      <td>
                        <div class="project-cell">
                          <app-avatar [name]="project.name" [square]="true" />
                          <div>
                            <strong class="cell-title">{{ project.name }}</strong>
                            <span class="cell-sub description">
                              {{ project.description || 'No description' }}
                            </span>
                          </div>
                        </div>
                      </td>
                      <td data-label="Status">
                        <span [class]="'pill project-status-' + project.status">
                          {{ statusLabel(project.status) }}
                        </span>
                      </td>
                      <td data-label="Created" class="date-cell">{{ project.createdAtUtc | utcDate:'mediumDate' }}</td>
                      <td class="col-actions">
                        <button type="button" class="btn btn-ghost btn-sm" (click)="openProject(project)">
                          View project
                          <app-icon name="chevronRight" [size]="16" />
                        </button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }

          <app-pagination [page]="data" noun="project" (pageChange)="changePage($event)" />
        }
      </section>
    </main>
  `,
  styles: [`
    :host { display:block; }
    .create-panel { max-width:720px; margin-bottom:20px; border-color:#d5daf6; box-shadow:0 1px 0 #d5daf6, 0 8px 24px -16px rgb(59 76 202 / .35); }
    .create-intro { display:flex; align-items:flex-start; gap:14px; margin-bottom:18px; }
    .create-icon { display:grid; place-items:center; flex-shrink:0; width:40px; height:40px; border-radius:10px; background:var(--c-primary-soft); color:var(--c-primary); }
    .optional { margin-left:4px; color:var(--c-faint); font-weight:500; }
    .project-cell { display:flex; align-items:center; gap:12px; min-width:0; }
    .project-cell > div { min-width:0; }
    .date-cell { color:var(--c-muted); white-space:nowrap; font-variant-numeric:tabular-nums; }

    @media (max-width: 720px) {
      .create-panel { max-width:none; }
      .project-cell { gap:10px; }
      .date-cell { white-space:normal; }
    }
  `]
})
export class ProjectsComponent {
  private readonly projects = inject(ProjectService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  readonly session = inject(SessionStore);

  readonly skeletonRows = [1, 2, 3, 4, 5];

  readonly result = signal<ProjectListResponse | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly searchText = signal('');
  readonly statusFilter = signal('');
  readonly sortBy = signal<ProjectListQuery['sortBy']>('createdAt');
  readonly showCreateForm = signal(false);
  readonly creating = signal(false);
  readonly createError = signal('');
  readonly pageNumber = signal(1);

  private readonly searchChanges = new Subject<string>();

  readonly createForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(150)]],
    description: ['', [Validators.maxLength(2000)]]
  });

  constructor() {
    this.searchChanges.pipe(
      debounceTime(350),
      distinctUntilChanged(),
      switchMap(search => {
        this.pageNumber.set(1);
        this.loading.set(true);
        this.error.set('');
        return this.projects.getProjects(this.query(search)).pipe(
          catchError(error => {
            this.handleLoadError(error);
            return of(null);
          })
        );
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(result => this.applyResult(result));

    this.loadProjects();
  }

  canCreate(): boolean {
    const roles = this.session.user()?.roles ?? [];
    return roles.includes('Admin') || roles.includes('Project Manager');
  }

  onSearch(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchText.set(value);
    this.searchChanges.next(value);
  }

  onStatusChange(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value);
    this.pageNumber.set(1);
    this.loadProjects();
  }

  onSortChange(event: Event): void {
    this.sortBy.set((event.target as HTMLSelectElement).value as ProjectListQuery['sortBy']);
    this.pageNumber.set(1);
    this.loadProjects();
  }

  loadProjects(): void {
    this.loading.set(true);
    this.error.set('');
    this.projects.getProjects(this.query()).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: result => this.applyResult(result),
      error: error => this.handleLoadError(error)
    });
  }

  changePage(page: number): void {
    this.pageNumber.set(page);
    this.loadProjects();
  }

  toggleCreateForm(): void {
    this.showCreateForm.update(value => !value);
    this.createError.set('');
    this.createForm.reset({ name: '', description: '' });
    if (this.showCreateForm()) {
      afterNextRender(() => document.getElementById('project-name')?.focus(), { injector: this.injector });
    }
  }

  createProject(): void {
    if (!this.canCreate() || this.createForm.invalid || this.creating()) {
      this.createForm.markAllAsTouched();
      return;
    }

    const value = this.createForm.getRawValue();
    this.creating.set(true);
    this.createError.set('');

    this.projects.createProject({
      name: value.name.trim(),
      description: value.description.trim() || null
    }).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: () => {
        this.creating.set(false);
        this.showCreateForm.set(false);
        this.createForm.reset({ name: '', description: '' });
        this.pageNumber.set(1);
        this.loadProjects();
      },
      error: error => {
        this.creating.set(false);
        this.createError.set(this.errorMessage(error, 'Could not create the project.'));
      }
    });
  }

  openProject(project: Project): void {
    void this.router.navigate(['/projects', project.id]);
  }

  statusLabel(status: ProjectStatus): string {
    return ({
      [ProjectStatus.Planning]: 'Planning',
      [ProjectStatus.Active]: 'Active',
      [ProjectStatus.Completed]: 'Completed',
      [ProjectStatus.OnHold]: 'On hold',
      [ProjectStatus.Archived]: 'Archived'
    } as Record<number, string>)[status] ?? 'Unknown';
  }

  private query(search = this.searchText()): ProjectListQuery {
    const status = this.statusFilter();
    return {
      pageNumber: this.pageNumber(),
      pageSize: 20,
      search,
      ...(status ? { status: Number(status) as ProjectStatus } : {}),
      sortBy: this.sortBy(),
      sortDescending: true
    };
  }

  private applyResult(result: ProjectListResponse | null): void {
    if (result) {
      this.result.set(result);
    }
    this.loading.set(false);
  }

  private handleLoadError(error: unknown): void {
    this.loading.set(false);
    this.error.set(this.errorMessage(error, 'Could not load projects. Please try again.'));
  }

  private errorMessage(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 403) {
        return 'You do not have permission to perform this action.';
      }
      if (error.status === 409) {
        return 'This project has changed. Reload it and try again.';
      }
    }
    return fallback;
  }
}
