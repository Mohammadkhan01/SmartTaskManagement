import { DatePipe } from '@angular/common';
import { UtcDatePipe } from '../../shared/pipes/utc-date.pipe';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
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
  TaskPriority,
  TaskStatus
} from '../../core/tasks/task.models';
import { TaskService } from '../../core/tasks/task.service';

@Component({
  selector: 'app-task-details',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [DatePipe],
  imports: [UtcDatePipe, RouterLink],
  template: `
    <main class="page">
      @if (task(); as data) {
        <a class="back" [routerLink]="['/projects', data.projectId, 'tasks']">← Back to tasks</a>
      } @else {
        <a class="back" routerLink="/projects">← Back to projects</a>
      }

      @if (loading()) {
        <p role="status">Loading task…</p>
      } @else if (error()) {
        <section class="panel">
          <p class="error" role="alert">{{ error() }}</p>
          <button type="button" (click)="load()">Try again</button>
        </section>
      } @else if (task(); as data) {
        <section class="panel">
          <div class="heading">
            <div>
              <p class="eyebrow">TASK DETAILS{{ project() ? ' · ' + project()!.name : '' }}</p>
              <h1>{{ data.title }}</h1>
              <span class="badge" [class]="'badge status-' + data.status">{{ statusLabel(data.status) }}</span>
              <span class="badge" [class]="'badge priority-' + data.priority">{{ priorityLabel(data.priority) }} priority</span>
            </div>
            @if (canManage()) {
              <div class="actions">
                <a class="button secondary" [routerLink]="['/tasks', data.id, 'edit']">Edit task</a>
                <button type="button" class="danger" [disabled]="deleting()" (click)="deleteTask()">
                  {{ deleting() ? 'Deleting…' : 'Delete task' }}
                </button>
              </div>
            }
          </div>

          <h2>Description</h2>
          <p class="description">{{ data.description || 'No description provided.' }}</p>

          <dl>
            <div><dt>Assignee</dt><dd>{{ assigneeName(data.assignedToUserId) }}</dd></div>
            <div><dt>Due date</dt><dd>{{ data.dueDateUtc ? (data.dueDateUtc | utcDate:'mediumDate':'UTC') : 'No due date' }}</dd></div>
            <div><dt>Created</dt><dd>{{ data.createdAtUtc | utcDate:'medium' }}</dd></div>
            <div><dt>Last updated</dt><dd>{{ data.updatedAtUtc ? (data.updatedAtUtc | utcDate:'medium') : '—' }}</dd></div>
          </dl>

          @if (actionError()) {
            <p class="error" role="alert">{{ actionError() }}</p>
          }
          @if (actionSuccess()) {
            <p class="success" role="status">{{ actionSuccess() }}</p>
          }
        </section>

        <section class="panel actions-panel">
          <div class="action-block">
            <h2>Change status</h2>
            @if (canChangeStatus()) {
              <div class="inline-form">
                <label class="sr-only" for="task-status">Status</label>
                <select id="task-status" (change)="onStatusSelect($event)">
                  @for (option of statusOptions; track option.value) {
                    <option [value]="option.value" [selected]="option.value === selectedStatus()">{{ option.label }}</option>
                  }
                </select>
                <button type="button" [disabled]="updatingStatus() || selectedStatus() === data.status"
                  (click)="updateStatus()">
                  {{ updatingStatus() ? 'Updating…' : 'Update status' }}
                </button>
              </div>
            } @else {
              <p class="muted">Only the assignee or a project manager can change this task's status.</p>
            }
          </div>

          <div class="action-block">
            <h2>Assign task</h2>
            @if (!canManage()) {
              <p class="muted">Only admins and project managers can assign tasks.</p>
            } @else if (project() && project()!.status !== activeStatus) {
              <p class="hint">
                The project is {{ projectStatusLabel(project()!.status) }}. Set it to Active on the
                <a [routerLink]="['/projects', data.projectId]">project page</a> before assigning tasks.
              </p>
            } @else if (members().length === 0) {
              <p class="muted">No project members are available to assign.</p>
            } @else {
              <div class="inline-form">
                <label class="sr-only" for="task-assignee">Assignee</label>
                <select id="task-assignee" (change)="onAssigneeSelect($event)">
                  <option value="" disabled [selected]="!selectedAssignee()">Select a member</option>
                  @for (member of members(); track member.userId) {
                    <option [value]="member.userId" [selected]="member.userId === selectedAssignee()">{{ member.displayName }}{{ member.userId === currentUserId() ? ' (you)' : '' }}</option>
                  }
                </select>
                <button type="button"
                  [disabled]="assigning() || !selectedAssignee() || selectedAssignee() === data.assignedToUserId"
                  (click)="assign()">
                  {{ assigning() ? 'Assigning…' : 'Assign' }}
                </button>
              </div>
            }
          </div>
        </section>
      }
    </main>
  `,
  styles: [`
    :host { display:block; }
    .page { max-width:900px; margin:auto; padding:24px 24px 48px; }
    .back { color:#1d4ed8; text-decoration:none; font-size:14px; }
    .panel { margin-top:18px; padding:28px; background:white; border:1px solid #e5eaf1; border-radius:12px; }
    .heading { display:flex; justify-content:space-between; align-items:flex-start; gap:20px; }
    .eyebrow { margin:0; color:#64748b; font-size:11px; font-weight:700; letter-spacing:1.2px; }
    h1 { margin:8px 0 14px; font-size:28px; }
    h2 { margin:22px 0 12px; font-size:17px; }
    .badge { display:inline-block; margin-right:8px; padding:5px 10px; border-radius:20px; background:#eef2f7; color:#475569; font-size:12px; }
    .status-2 { background:#dbeafe; color:#1d4ed8; }
    .status-3 { background:#dcfce7; color:#166534; }
    .priority-3 { background:#ffedd5; color:#9a3412; }
    .priority-4 { background:#fee2e2; color:#b91c1c; }
    .description { color:#475569; line-height:1.6; white-space:pre-wrap; }
    .actions { display:flex; gap:8px; flex-wrap:wrap; }
    dl { margin-top:24px; }
    dl div { display:grid; grid-template-columns:140px 1fr; gap:12px; padding:12px 0; border-top:1px solid #edf0f4; }
    dt { color:#64748b; font-size:13px; }
    dd { margin:0; font-size:13px; overflow-wrap:anywhere; }
    .actions-panel { display:grid; grid-template-columns:1fr 1fr; gap:28px; }
    .actions-panel h2 { margin-top:0; }
    .inline-form { display:flex; gap:10px; }
    select { flex:1; min-width:0; border:1px solid #cbd5e1; border-radius:7px; padding:10px 12px; background:white; font:inherit; }
    button, a.button { border:0; border-radius:8px; padding:10px 16px; background:#1d4ed8; color:white; cursor:pointer; text-decoration:none; font-size:14px; white-space:nowrap; }
    button:disabled { opacity:.55; cursor:not-allowed; }
    .secondary, a.button.secondary { background:#e2e8f0; color:#1e293b; }
    .danger { background:#b91c1c; }
    .error { color:#b91c1c; font-size:13px; }
    .success { color:#166534; font-size:13px; }
    .muted { color:#64748b; font-size:13px; }
    .hint { padding:10px 12px; border-radius:8px; background:#fef3c7; color:#92400e; font-size:13px; }
    .hint a { color:#92400e; font-weight:600; }
    .sr-only { position:absolute; width:1px; height:1px; overflow:hidden; clip:rect(0 0 0 0); }
    @media(max-width:700px) {
      .heading { flex-direction:column; }
      .actions-panel { grid-template-columns:1fr; }
      dl div { grid-template-columns:100px 1fr; }
    }
  `]
})
export class TaskDetailsComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly tasks = inject(TaskService);
  private readonly projects = inject(ProjectService);
  private readonly session = inject(SessionStore);
  private readonly destroyRef = inject(DestroyRef);

  private readonly taskId = this.route.snapshot.paramMap.get('taskId');
  readonly activeStatus = ProjectStatus.Active;

  readonly task = signal<Task | null>(null);
  readonly project = signal<Project | null>(null);
  readonly members = signal<ProjectMember[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly actionError = signal('');
  readonly actionSuccess = signal('');
  readonly deleting = signal(false);
  readonly updatingStatus = signal(false);
  readonly assigning = signal(false);
  readonly selectedStatus = signal<TaskStatus>(TaskStatus.ToDo);
  readonly selectedAssignee = signal('');

  readonly currentUserId = computed(() => this.session.user()?.userId ?? '');
  readonly canManage = computed(() =>
    this.session.hasRole('Admin') || this.session.hasRole('Project Manager'));
  readonly canChangeStatus = computed(() =>
    this.canManage() || this.task()?.assignedToUserId === this.currentUserId());

  readonly statusOptions = Object.entries(TASK_STATUS_LABELS).map(([value, label]) => ({
    value: Number(value) as TaskStatus,
    label
  }));

  constructor() {
    this.load();
  }

  load(): void {
    if (!this.taskId) {
      this.loading.set(false);
      this.error.set('A valid task ID is required.');
      return;
    }

    this.loading.set(true);
    this.error.set('');

    this.tasks.getTask(this.taskId).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: task => {
        this.applyTask(task);
        this.loading.set(false);
        this.loadProjectContext(task.projectId);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.error.set(
          error instanceof HttpErrorResponse && error.status === 404
            ? 'Task not found or you do not have access to it.'
            : describeHttpError(error, { fallback: 'Could not load the task. Please try again.' })
        );
      }
    });
  }

  onStatusSelect(event: Event): void {
    this.selectedStatus.set(Number((event.target as HTMLSelectElement).value) as TaskStatus);
  }

  onAssigneeSelect(event: Event): void {
    this.selectedAssignee.set((event.target as HTMLSelectElement).value);
  }

  updateStatus(): void {
    const current = this.task();
    if (!current || this.updatingStatus()) return;

    this.updatingStatus.set(true);
    this.clearMessages();

    this.tasks.updateStatus(current.id, {
      status: this.selectedStatus(),
      rowVersion: current.rowVersion
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: task => {
        this.applyTask(task);
        this.updatingStatus.set(false);
        this.actionSuccess.set(`Status changed to ${this.statusLabel(task.status)}.`);
      },
      error: (error: unknown) => {
        this.updatingStatus.set(false);
        this.actionError.set(describeHttpError(error, {
          notFound: 'You cannot change the status of this task, or it no longer exists.',
          conflict: 'This task was changed by someone else. Reload the page and try again.',
          fallback: 'Could not update the status. Please try again.'
        }));
      }
    });
  }

  assign(): void {
    const current = this.task();
    const assignee = this.selectedAssignee();
    if (!current || !assignee || this.assigning()) return;

    this.assigning.set(true);
    this.clearMessages();

    this.tasks.assignTask(current.id, {
      assignedToUserId: assignee,
      rowVersion: current.rowVersion
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: task => {
        this.applyTask(task);
        this.assigning.set(false);
        this.actionSuccess.set(`Task assigned to ${this.assigneeName(task.assignedToUserId)}.`);
      },
      error: (error: unknown) => {
        this.assigning.set(false);
        this.actionError.set(describeHttpError(error, {
          badRequest: 'The selected user cannot be assigned to this task.',
          notFound: 'The task could not be assigned. The project must be Active and you must manage it.',
          conflict: 'This task was changed by someone else. Reload the page and try again.',
          fallback: 'Could not assign the task. Please try again.'
        }));
      }
    });
  }

  deleteTask(): void {
    const current = this.task();
    if (!current || this.deleting()) return;
    if (!window.confirm(`Delete the task "${current.title}"? This action cannot be undone.`)) return;

    this.deleting.set(true);
    this.clearMessages();

    this.tasks.deleteTask(current.id).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: () => void this.router.navigate(['/projects', current.projectId, 'tasks']),
      error: (error: unknown) => {
        this.deleting.set(false);
        this.actionError.set(describeHttpError(error, {
          notFound: 'This task no longer exists or you cannot delete it.',
          fallback: 'Could not delete the task. Please try again.'
        }));
      }
    });
  }

  assigneeName(userId: string | null): string {
    if (!userId) return 'Unassigned';
    if (userId === this.currentUserId()) return `${this.session.user()?.displayName ?? 'You'} (you)`;
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

  private applyTask(task: Task): void {
    this.task.set(task);
    this.selectedStatus.set(task.status);
    this.selectedAssignee.set(task.assignedToUserId ?? '');
  }

  private loadProjectContext(projectId: string): void {
    this.projects.getProject(projectId).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: project => this.project.set(project),
      error: () => this.project.set(null)
    });

    this.projects.getMembers(projectId).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: members => this.members.set(members),
      error: () => this.members.set([])
    });
  }

  private clearMessages(): void {
    this.actionError.set('');
    this.actionSuccess.set('');
  }
}
