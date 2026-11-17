import { DatePipe } from '@angular/common';
import { UtcDatePipe } from '../../shared/pipes/utc-date.pipe';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/api-error';
import { SessionStore } from '../../core/auth/session-store';
import {
  PROJECT_STATUS_LABELS,
  Project,
  ProjectMember,
  ProjectStatus
} from '../../core/projects/project.models';
import { ProjectService } from '../../core/projects/project.service';
import {
  TASK_PRIORITY_LABELS,
  TASK_STATUS_LABELS,
  Task,
  TaskListQuery,
  TaskListResponse,
  TaskPriority,
  TaskStatus
} from '../../core/tasks/task.models';
import { TaskService } from '../../core/tasks/task.service';

@Component({
  selector: 'app-task-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [DatePipe],
  imports: [UtcDatePipe, RouterLink],
  template: `
    <main class="page">
      <a class="back" [routerLink]="['/projects', projectId]">← Back to project</a>

      <section class="heading">
        <div>
          <p class="eyebrow">PROJECT TASKS</p>
          <h1>{{ project()?.name ?? 'Tasks' }}</h1>
          @if (project(); as data) {
            <p class="subtitle">
              Project status: <strong>{{ projectStatusLabel(data.status) }}</strong>
              @if (data.status !== activeStatus) {
                · Tasks can only be assigned once the project is Active.
              }
            </p>
          }
        </div>
        @if (canManage()) {
          <a class="button" [routerLink]="['/projects', projectId, 'tasks', 'new']">Create task</a>
        }
      </section>

      <section class="panel">
        <div class="filters">
          <label class="search">
            <span>Search tasks</span>
            <input type="search" placeholder="Search by title or description"
              [value]="searchText()" (input)="onSearch($event)">
          </label>
          <label>
            <span>Status</span>
            <select (change)="onStatusChange($event)">
              <option value="">All statuses</option>
              @for (option of statusOptions; track option.value) {
                <option [value]="option.value" [selected]="option.value === statusFilter()">{{ option.label }}</option>
              }
            </select>
          </label>
          <label>
            <span>Priority</span>
            <select (change)="onPriorityChange($event)">
              <option value="">All priorities</option>
              @for (option of priorityOptions; track option.value) {
                <option [value]="option.value" [selected]="option.value === priorityFilter()">{{ option.label }}</option>
              }
            </select>
          </label>
          <label>
            <span>Sort by</span>
            <select [value]="sortBy()" (change)="onSortChange($event)">
              <option value="createdAt">Date created</option>
              <option value="updatedAt">Date updated</option>
              <option value="dueDate">Due date</option>
              <option value="title">Title</option>
              <option value="priority">Priority</option>
              <option value="status">Status</option>
            </select>
          </label>
          <label>
            <span>Order</span>
            <select [value]="sortDescending() ? 'desc' : 'asc'" (change)="onOrderChange($event)">
              <option value="desc">Descending</option>
              <option value="asc">Ascending</option>
            </select>
          </label>
        </div>

        @if (actionError()) {
          <p class="error" role="alert">{{ actionError() }}</p>
        }

        @if (loading()) {
          <p class="message" role="status">Loading tasks…</p>
        } @else if (error()) {
          <div class="message">
            <p class="error" role="alert">{{ error() }}</p>
            <button type="button" class="secondary" (click)="loadTasks()">Try again</button>
          </div>
        } @else if (result(); as data) {
          @if (data.items.length === 0) {
            <div class="empty">
              <h2>No tasks found</h2>
              <p>{{ hasFilters() ? 'Try changing your search or filters.' : 'This project has no tasks yet.' }}</p>
            </div>
          } @else {
            <div class="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th scope="col">Task</th>
                    <th scope="col">Status</th>
                    <th scope="col">Priority</th>
                    <th scope="col">Assignee</th>
                    <th scope="col">Due</th>
                    <th scope="col">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  @for (task of data.items; track task.id) {
                    <tr>
                      <td>
                        <a class="title" [routerLink]="['/tasks', task.id]">{{ task.title }}</a>
                        <span class="description">{{ task.description || 'No description' }}</span>
                      </td>
                      <td><span class="badge" [class]="'badge status-' + task.status">{{ statusLabel(task.status) }}</span></td>
                      <td><span class="badge" [class]="'badge priority-' + task.priority">{{ priorityLabel(task.priority) }}</span></td>
                      <td>{{ assigneeName(task.assignedToUserId) }}</td>
                      <td>{{ task.dueDateUtc ? (task.dueDateUtc | utcDate:'mediumDate':'UTC') : '—' }}</td>
                      <td class="row-actions">
                        <a [routerLink]="['/tasks', task.id]">View</a>
                        @if (canManage()) {
                          <a [routerLink]="['/tasks', task.id, 'edit']">Edit</a>
                          <button type="button" class="link-button danger"
                            [disabled]="deletingId() === task.id" (click)="deleteTask(task)">
                            {{ deletingId() === task.id ? 'Deleting…' : 'Delete' }}
                          </button>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }

          <footer class="pagination">
            <span>
              {{ data.totalCount }} task(s) · Page {{ data.pageNumber }} of {{ data.totalPages || 1 }}
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
    :host { display:block; }
    .page { max-width:1180px; margin:auto; padding:24px 28px 48px; }
    .back { color:#1d4ed8; text-decoration:none; font-size:14px; }
    .heading { display:flex; justify-content:space-between; align-items:center; gap:20px; margin:18px 0 24px; }
    h1 { margin:5px 0 6px; font-size:30px; }
    h2 { margin:0 0 8px; font-size:20px; }
    .eyebrow { margin:0; color:#64748b; font-size:11px; font-weight:700; letter-spacing:1.3px; }
    .subtitle { margin:0; color:#64748b; font-size:14px; }
    button, a.button { border:0; border-radius:8px; padding:11px 17px; background:#1d4ed8; color:white; cursor:pointer; text-decoration:none; font-size:14px; }
    button:disabled { opacity:.5; cursor:not-allowed; }
    .secondary { background:#eef2f7; color:#334155; }
    .panel { background:white; border:1px solid #e5eaf1; border-radius:12px; padding:22px; }
    label { color:#475569; font-size:13px; font-weight:600; }
    input, select { width:100%; border:1px solid #cbd5e1; border-radius:7px; padding:10px 12px; color:#17243b; background:white; }
    input:focus, select:focus { outline:2px solid #93c5fd; border-color:#2563eb; }
    .filters { display:grid; grid-template-columns:minmax(220px,1fr) repeat(4, 150px); gap:16px; margin-bottom:22px; }
    .filters label { display:grid; gap:7px; }
    .error { color:#b91c1c; font-size:13px; }
    .message { padding:22px 0; color:#64748b; }
    .empty { text-align:center; padding:42px 16px; color:#64748b; }
    .empty h2 { color:#17243b; }
    .table-wrap { overflow-x:auto; }
    table { width:100%; border-collapse:collapse; text-align:left; }
    th { color:#64748b; font-size:12px; font-weight:600; }
    th,td { padding:15px 12px; border-bottom:1px solid #edf0f4; }
    td { font-size:13px; }
    .title { display:block; color:#17243b; font-weight:600; text-decoration:none; }
    .title:hover { color:#1d4ed8; }
    .description { display:block; max-width:380px; margin-top:5px; color:#64748b; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .badge { display:inline-block; padding:5px 9px; border-radius:20px; background:#eef2f7; color:#475569; font-size:12px; white-space:nowrap; }
    .status-2 { background:#dbeafe; color:#1d4ed8; }
    .status-3 { background:#dcfce7; color:#166534; }
    .status-4 { background:#f1f5f9; color:#64748b; }
    .priority-3 { background:#ffedd5; color:#9a3412; }
    .priority-4 { background:#fee2e2; color:#b91c1c; }
    .row-actions { display:flex; gap:12px; align-items:center; white-space:nowrap; }
    .row-actions a { color:#1d4ed8; text-decoration:none; }
    .link-button { padding:0; background:transparent; color:#1d4ed8; font-size:13px; }
    .link-button.danger { color:#b91c1c; }
    .pagination { display:flex; justify-content:space-between; align-items:center; gap:16px; padding-top:20px; color:#64748b; font-size:13px; }
    .pagination div { display:flex; gap:8px; }
    @media(max-width:900px) {
      .page { padding:16px; }
      .heading { align-items:flex-start; flex-direction:column; }
      .filters { grid-template-columns:1fr 1fr; }
      .filters .search { grid-column:1 / -1; }
      .pagination { align-items:flex-start; flex-direction:column; }
    }
  `]
})
export class TaskListComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly tasks = inject(TaskService);
  private readonly projects = inject(ProjectService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly session = inject(SessionStore);

  readonly projectId = this.route.snapshot.paramMap.get('id') ?? '';
  readonly activeStatus = ProjectStatus.Active;

  readonly project = signal<Project | null>(null);
  readonly members = signal<ProjectMember[]>([]);
  readonly result = signal<TaskListResponse | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly actionError = signal('');
  readonly deletingId = signal('');

  readonly searchText = signal('');
  readonly statusFilter = signal('');
  readonly priorityFilter = signal('');
  readonly sortBy = signal<TaskListQuery['sortBy']>('createdAt');
  readonly sortDescending = signal(true);
  readonly pageNumber = signal(1);

  readonly canManage = computed(() =>
    this.session.hasRole('Admin') || this.session.hasRole('Project Manager'));
  readonly hasFilters = computed(() =>
    !!this.searchText().trim() || !!this.statusFilter() || !!this.priorityFilter());

  readonly statusOptions = Object.entries(TASK_STATUS_LABELS).map(([value, label]) => ({ value, label }));
  readonly priorityOptions = Object.entries(TASK_PRIORITY_LABELS).map(([value, label]) => ({ value, label }));

  private readonly searchChanges = new Subject<string>();

  constructor() {
    this.searchChanges.pipe(
      debounceTime(350),
      distinctUntilChanged(),
      switchMap(search => {
        this.pageNumber.set(1);
        this.loading.set(true);
        this.error.set('');
        return this.tasks.getTasks(this.projectId, this.query(search)).pipe(
          catchError(error => {
            this.handleLoadError(error);
            return of(null);
          })
        );
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(result => this.applyResult(result));

    this.loadProject();
    this.loadTasks();
  }

  onSearch(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchText.set(value);
    this.searchChanges.next(value);
  }

  onStatusChange(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value);
    this.reload();
  }

  onPriorityChange(event: Event): void {
    this.priorityFilter.set((event.target as HTMLSelectElement).value);
    this.reload();
  }

  onSortChange(event: Event): void {
    this.sortBy.set((event.target as HTMLSelectElement).value as TaskListQuery['sortBy']);
    this.reload();
  }

  onOrderChange(event: Event): void {
    this.sortDescending.set((event.target as HTMLSelectElement).value === 'desc');
    this.reload();
  }

  changePage(page: number): void {
    this.pageNumber.set(page);
    this.loadTasks();
  }

  loadTasks(): void {
    if (!this.projectId) {
      this.error.set('A valid project ID is required.');
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.tasks.getTasks(this.projectId, this.query()).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: result => this.applyResult(result),
      error: error => this.handleLoadError(error)
    });
  }

  deleteTask(task: Task): void {
    if (this.deletingId()) return;
    if (!window.confirm(`Delete the task "${task.title}"? This action cannot be undone.`)) return;

    this.deletingId.set(task.id);
    this.actionError.set('');

    this.tasks.deleteTask(task.id).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: () => {
        this.deletingId.set('');
        this.loadTasks();
      },
      error: error => {
        this.deletingId.set('');
        this.actionError.set(describeHttpError(error, {
          forbidden: 'You do not have permission to delete this task.',
          notFound: 'This task no longer exists or you cannot delete it.',
          fallback: 'Could not delete the task. Please try again.'
        }));
      }
    });
  }

  assigneeName(userId: string | null): string {
    if (!userId) return 'Unassigned';
    if (userId === this.session.user()?.userId) return 'You';
    return this.members().find(member => member.userId === userId)?.displayName ?? 'Project member';
  }

  statusLabel(status: TaskStatus): string {
    return TASK_STATUS_LABELS[status] ?? 'Unknown';
  }

  priorityLabel(priority: TaskPriority): string {
    return TASK_PRIORITY_LABELS[priority] ?? 'Unknown';
  }

  projectStatusLabel(status: ProjectStatus): string {
    return PROJECT_STATUS_LABELS[status] ?? 'Unknown';
  }

  private loadProject(): void {
    if (!this.projectId) return;

    this.projects.getProject(this.projectId).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: project => this.project.set(project),
      error: () => this.project.set(null)
    });

    // Member names are only visible to project managers; others see generic labels.
    this.projects.getMembers(this.projectId).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: members => this.members.set(members),
      error: () => this.members.set([])
    });
  }

  private reload(): void {
    this.pageNumber.set(1);
    this.loadTasks();
  }

  private query(search = this.searchText()): TaskListQuery {
    const status = this.statusFilter();
    const priority = this.priorityFilter();
    return {
      pageNumber: this.pageNumber(),
      pageSize: 20,
      search,
      ...(status ? { status: Number(status) as TaskStatus } : {}),
      ...(priority ? { priority: Number(priority) as TaskPriority } : {}),
      sortBy: this.sortBy(),
      sortDescending: this.sortDescending()
    };
  }

  private applyResult(result: TaskListResponse | null): void {
    if (result) {
      this.result.set(result);
    }
    this.loading.set(false);
  }

  private handleLoadError(error: unknown): void {
    this.loading.set(false);
    this.error.set(
      error instanceof HttpErrorResponse && error.status === 404
        ? 'Project not found or you do not have access to its tasks.'
        : describeHttpError(error, { fallback: 'Could not load tasks. Please try again.' })
    );
  }
}
