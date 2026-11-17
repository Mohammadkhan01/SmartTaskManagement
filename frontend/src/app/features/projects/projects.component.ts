import { DatePipe } from '@angular/common';
import { UtcDatePipe } from '../../shared/pipes/utc-date.pipe';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
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

@Component({
  selector: 'app-projects',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [DatePipe],
  imports: [UtcDatePipe, ReactiveFormsModule],
  template: `
    <main class="page">
      <section class="heading">
        <div>
          <p class="eyebrow">WORKSPACE</p>
          <h1>Projects</h1>
          <p class="subtitle">View and manage the projects you belong to.</p>
        </div>
        @if (canCreate()) {
          <button type="button" (click)="toggleCreateForm()">
            {{ showCreateForm() ? 'Cancel' : 'Create project' }}
          </button>
        }
      </section>

      @if (showCreateForm()) {
        <section class="panel create-panel">
          <h2>Create a project</h2>
          <form [formGroup]="createForm" (ngSubmit)="createProject()">
            <label for="project-name">Project name</label>
            <input id="project-name" type="text" formControlName="name"
              maxlength="150" autocomplete="off">
            @if (createForm.controls.name.touched && createForm.controls.name.invalid) {
              <p class="field-error">Enter a project name (maximum 150 characters).</p>
            }

            <label for="project-description">Description</label>
            <textarea id="project-description" rows="3"
              formControlName="description" maxlength="2000"></textarea>

            @if (createError()) {
              <p class="error" role="alert">{{ createError() }}</p>
            }

            <div class="form-actions">
              <button type="submit" [disabled]="creating() || createForm.invalid">
                {{ creating() ? 'Creating…' : 'Create project' }}
              </button>
              <button type="button" class="secondary" (click)="toggleCreateForm()">
                Cancel
              </button>
            </div>
          </form>
        </section>
      }

      <section class="panel">
        <div class="filters">
          <label class="search">
            <span>Search projects</span>
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
          <p class="message" role="status">Loading projects…</p>
        } @else if (error()) {
          <div class="message">
            <p class="error" role="alert">{{ error() }}</p>
            <button type="button" class="secondary" (click)="loadProjects()">Try again</button>
          </div>
        } @else if (result(); as data) {
          @if (data.items.length === 0) {
            <div class="empty">
              <h2>No projects found</h2>
              <p>Try changing your search or status filter.</p>
            </div>
          } @else {
            <div class="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th scope="col">Project</th>
                    <th scope="col">Status</th>
                    <th scope="col">Created</th>
                    <th scope="col">Action</th>
                  </tr>
                </thead>
                <tbody>
                  @for (project of data.items; track project.id) {
                    <tr>
                      <td>
                        <strong>{{ project.name }}</strong>
                        <span class="description">
                          {{ project.description || 'No description' }}
                        </span>
                      </td>
                      <td>
                        <span class="status" [class]="'status status-' + project.status">
                          {{ statusLabel(project.status) }}
                        </span>
                      </td>
                      <td>{{ project.createdAtUtc | utcDate:'mediumDate' }}</td>
                      <td>
                        <button type="button" class="link-button"
                          (click)="openProject(project)">View project</button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }

          <footer class="pagination">
            <span>
              {{ data.totalCount }} project(s) · Page {{ data.pageNumber }} of {{ data.totalPages || 1 }}
            </span>
            <div>
              <button type="button" class="secondary" (click)="changePage(data.pageNumber - 1)"
                [disabled]="!data.hasPreviousPage">Previous</button>
              <button type="button" class="secondary" (click)="changePage(data.pageNumber + 1)"
                [disabled]="!data.hasNextPage">Next</button>
            </div>
          </footer>
        }
      </section>
    </main>
  `,
  styles: [`
    :host { display:block; min-height:100vh; background:#f4f6fa; }
    .page { max-width:1180px; margin:auto; padding:24px 28px 48px; }
    .heading { display:flex; justify-content:space-between; align-items:center; gap:20px; margin:10px 0 24px; }
    h1 { margin:5px 0 6px; font-size:32px; }
    h2 { margin:0 0 20px; font-size:20px; }
    .eyebrow { margin:0; color:#64748b; font-size:11px; font-weight:700; letter-spacing:1.3px; }
    .subtitle { margin:0; color:#64748b; font-size:14px; }
    button { border:0; border-radius:8px; padding:11px 17px; background:#1d4ed8; color:white; cursor:pointer; }
    button:disabled { opacity:.5; cursor:not-allowed; }
    .secondary { background:#eef2f7; color:#334155; }
    .panel { background:white; border:1px solid #e5eaf1; border-radius:12px; padding:22px; }
    .create-panel { margin-bottom:20px; max-width:680px; }
    form { display:grid; gap:9px; }
    label { color:#475569; font-size:13px; font-weight:600; }
    input, textarea, select { width:100%; border:1px solid #cbd5e1; border-radius:7px; padding:10px 12px; color:#17243b; background:white; }
    input:focus, textarea:focus, select:focus { outline:2px solid #93c5fd; border-color:#2563eb; }
    textarea { resize:vertical; }
    .form-actions { display:flex; gap:10px; margin-top:10px; }
    .field-error,.error { color:#b91c1c; font-size:13px; }
    .filters { display:grid; grid-template-columns:minmax(220px,1fr) 180px 180px; gap:16px; margin-bottom:22px; }
    .filters label { display:grid; gap:7px; }
    .message { padding:22px 0; color:#64748b; }
    .empty { text-align:center; padding:42px 16px; color:#64748b; }
    .empty h2 { color:#17243b; margin-bottom:8px; }
    .table-wrap { overflow-x:auto; }
    table { width:100%; border-collapse:collapse; text-align:left; }
    th { color:#64748b; font-size:12px; font-weight:600; }
    th,td { padding:15px 12px; border-bottom:1px solid #edf0f4; }
    td { font-size:13px; }
    td strong,.description { display:block; }
    .description { max-width:440px; margin-top:5px; color:#64748b; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .status { display:inline-block; padding:5px 9px; border-radius:20px; background:#eef2f7; color:#475569; font-size:12px; white-space:nowrap; }
    .status-2 { background:#dbeafe; color:#1d4ed8; }
    .status-3 { background:#dcfce7; color:#166534; }
    .status-4 { background:#fef3c7; color:#92400e; }
    .status-5 { background:#f1f5f9; color:#64748b; }
    .link-button { padding:5px 0; background:transparent; color:#1d4ed8; }
    .pagination { display:flex; justify-content:space-between; align-items:center; gap:16px; padding-top:20px; color:#64748b; font-size:13px; }
    .pagination div { display:flex; gap:8px; }
    @media(max-width:720px) {
      .page { padding:16px; }
      .heading { align-items:flex-start; flex-direction:column; }
      .filters { grid-template-columns:1fr; }
      .pagination { align-items:flex-start; flex-direction:column; }
    }
  `]
})
export class ProjectsComponent {
  private readonly projects = inject(ProjectService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  readonly session = inject(SessionStore);

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
