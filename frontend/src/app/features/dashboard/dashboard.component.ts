import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { SessionStore } from '../../core/auth/session-store';
import {
  AdminDashboard,
  DashboardProjectTotals,
  DashboardSummary,
  DashboardTaskItem,
  DashboardTaskStatistics
} from '../../core/dashboard/dashboard.models';
import { DashboardService } from '../../core/dashboard/dashboard.service';
import {
  TASK_PRIORITY_LABELS,
  TASK_STATUS_LABELS,
  TaskPriority,
  TaskStatus
} from '../../core/tasks/task.models';
import { DueState, dueState } from '../../shared/due-date';
import { UtcDatePipe } from '../../shared/pipes/utc-date.pipe';
import { IconComponent } from '../../shared/ui/icon.component';

interface StatusRow {
  label: string;
  count: number;
  /** Status tone class from the design system, e.g. `task-status-2`. */
  tone: string;
}

interface RingSegment {
  tone: string;
  dash: string;
  offset: number;
}

const RING_RADIUS = 52;
const RING_LENGTH = 2 * Math.PI * RING_RADIUS;

@Component({
  selector: 'app-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [DatePipe],
  imports: [DatePipe, NgTemplateOutlet, UtcDatePipe, RouterLink, IconComponent],
  template: `
    <main class="page">
      <header class="page-header">
        <div>
          <h1>{{ greeting() }}</h1>
          <p class="page-subtitle">{{ today | date:'EEEE, d MMMM y' }}</p>
        </div>
        <a class="btn btn-secondary" routerLink="/projects">
          <app-icon name="folder" [size]="16" />
          Go to projects
        </a>
      </header>

      @if (loading()) {
        <div class="loading" aria-busy="true">
          <p class="message" role="status">Loading your dashboard…</p>
          <span class="skeleton kpi-skeleton"></span>
          <div class="dash-grid">
            <span class="skeleton block span-7"></span>
            <span class="skeleton block span-5"></span>
          </div>
        </div>
      } @else if (error()) {
        <section class="panel">
          <div class="empty-state">
            <span class="empty-state-icon tone-danger"><app-icon name="alert" /></span>
            <p class="error" role="alert">{{ error() }}</p>
            <button type="button" class="btn btn-primary" (click)="load()">
              <app-icon name="refresh" [size]="16" />
              Try again
            </button>
          </div>
        </section>
      } @else if (summary(); as data) {
        <section class="metrics" aria-label="Task summary">
          @if (data.projectTotals; as projects) {
            <article class="kpi tone-primary">
              <span class="kpi-label">Total projects</span>
              <strong class="kpi-value">{{ projects.totalProjects }}</strong>
              <small class="kpi-note">{{ projects.activeProjects }} active</small>
              <app-icon class="kpi-icon" name="folder" />
            </article>
          }
          <article class="kpi tone-neutral">
            <span class="kpi-label">Total tasks</span>
            <strong class="kpi-value">{{ data.totalTasks }}</strong>
            <small class="kpi-note">{{ data.inProgressTasks }} in progress</small>
            <app-icon class="kpi-icon" name="listChecks" />
          </article>
          <article class="kpi tone-success">
            <span class="kpi-label">Completed</span>
            <strong class="kpi-value">{{ data.completedTasks }}</strong>
            <small class="kpi-note">{{ data.completionPercentage }}% of all tasks</small>
            <app-icon class="kpi-icon" name="checkCircle" />
          </article>
          <article class="kpi tone-info">
            <span class="kpi-label">Pending</span>
            <strong class="kpi-value">{{ pendingTasks(data) }}</strong>
            <small class="kpi-note">{{ data.toDoTasks }} not started</small>
            <app-icon class="kpi-icon" name="clock" />
          </article>
          <article class="kpi" [class.tone-danger]="data.overdueTasks > 0" [class.tone-neutral]="data.overdueTasks === 0"
            [class.flagged]="data.overdueTasks > 0">
            <span class="kpi-label">Overdue</span>
            <strong class="kpi-value">{{ data.overdueTasks }}</strong>
            <small class="kpi-note">{{ data.overdueTasks > 0 ? 'Past their due date' : 'Nothing overdue' }}</small>
            <app-icon class="kpi-icon" name="alert" />
          </article>
          <article class="kpi" [class.tone-warning]="data.dueTodayTasks > 0" [class.tone-neutral]="data.dueTodayTasks === 0">
            <span class="kpi-label">Due today</span>
            <strong class="kpi-value">{{ data.dueTodayTasks }}</strong>
            <small class="kpi-note">{{ data.dueTodayTasks > 0 ? 'Due by end of day' : 'Nothing due today' }}</small>
            <app-icon class="kpi-icon" name="calendar" />
          </article>
        </section>

        <div class="dash-grid">
          <section class="panel" [class.span-7]="statistics()" [class.span-12]="!statistics()" aria-label="Tasks by status">
            <div class="panel-header">
              <div>
                <h2>Tasks by status</h2>
                <p class="panel-meta">{{ data.completedTasks }} completed vs {{ pendingTasks(data) }} pending</p>
              </div>
            </div>
            <div class="status-overview">
              <figure class="ring" role="img" [attr.aria-label]="data.completionPercentage + '% of tasks complete'">
                <svg viewBox="0 0 120 120" aria-hidden="true">
                  <circle class="ring-track" cx="60" cy="60" [attr.r]="ringRadius" />
                  @for (segment of statusSegments(); track segment.tone) {
                    <circle [class]="'ring-segment ' + segment.tone" cx="60" cy="60" [attr.r]="ringRadius"
                      [attr.stroke-dasharray]="segment.dash" [attr.stroke-dashoffset]="segment.offset" />
                  }
                </svg>
                <figcaption>
                  <strong class="ring-value">{{ data.completionPercentage }}%</strong>
                  <span>complete</span>
                </figcaption>
              </figure>
              <div class="status-grid">
                @for (item of taskStatusRows(data); track item.label) {
                  <div [class]="'status-chip ' + item.tone">
                    <span>{{ item.label }}</span><strong>{{ item.count }}</strong>
                    <small>{{ share(item.count, data.totalTasks) }}%</small>
                  </div>
                }
              </div>
            </div>
          </section>

          @if (statistics(); as stats) {
            <section class="panel span-5" aria-label="Priority breakdown">
              <div class="panel-header">
                <div>
                  <h2>Tasks by priority</h2>
                  <p class="panel-meta">{{ stats.pendingTasks }} pending</p>
                </div>
              </div>
              <div class="priority-grid">
                @for (item of priorityRows(stats); track item.label) {
                  <div [class]="'priority-row priority-' + item.priority">
                    <span class="priority-label">{{ item.label }}</span>
                    <div class="bar" role="presentation">
                      <div class="fill" [style.width.%]="barWidth(item.count, stats)"></div>
                    </div>
                    <strong>{{ item.count }}</strong>
                  </div>
                }
              </div>
            </section>
          }

          @if (data.projectTotals; as projects) {
            <section class="panel span-12" aria-label="Project summary">
              <div class="panel-header">
                <div>
                  <h2>Projects by status</h2>
                  <p class="panel-meta">{{ projects.totalProjects }} total</p>
                </div>
                <a class="btn btn-ghost btn-sm" routerLink="/projects">
                  View all <app-icon name="chevronRight" [size]="16" />
                </a>
              </div>
              <div class="ribbon" role="presentation">
                @for (item of projectStatusRows(projects); track item.label) {
                  @if (item.count > 0) {
                    <span [class]="'ribbon-segment ' + item.tone" [style.flex-grow]="item.count"></span>
                  }
                }
              </div>
              <div class="status-grid wide">
                @for (item of projectStatusRows(projects); track item.label) {
                  <div [class]="'status-chip ' + item.tone">
                    <span>{{ item.label }}</span><strong>{{ item.count }}</strong>
                    <small>{{ share(item.count, projects.totalProjects) }}%</small>
                  </div>
                }
              </div>
            </section>
          }

          <section class="panel span-6">
            <div class="panel-header"><h2>Recent tasks</h2></div>
            @if (data.recentTasks.length === 0) {
              <div class="empty-state compact">
                <span class="empty-state-icon"><app-icon name="inbox" /></span>
                <p>No recent tasks found.</p>
                <a routerLink="/projects">Open a project to add or pick up work</a>
              </div>
            } @else {
              <ul class="task-list" role="list">
                @for (task of data.recentTasks; track task.id) {
                  <ng-container *ngTemplateOutlet="taskRow; context: { $implicit: task }" />
                }
              </ul>
            }
          </section>

          <section class="panel span-6">
            <div class="panel-header"><h2>Upcoming tasks</h2></div>
            @if (!statistics()) {
              <div class="empty-state compact">
                <span class="empty-state-icon tone-warning"><app-icon name="alert" /></span>
                <p>Upcoming tasks are unavailable right now.</p>
              </div>
            } @else if (statistics()!.upcomingTasks.length === 0) {
              <div class="empty-state compact">
                <span class="empty-state-icon"><app-icon name="calendar" /></span>
                <p>No upcoming due dates.</p>
              </div>
            } @else {
              <ul class="task-list" role="list">
                @for (task of statistics()!.upcomingTasks; track task.id) {
                  <ng-container *ngTemplateOutlet="taskRow; context: { $implicit: task }" />
                }
              </ul>
            }
          </section>

          @if (adminDashboard(); as admin) {
            <section class="panel admin-overview span-12" aria-label="System overview">
              <div class="panel-header">
                <div>
                  <span class="admin-tag"><app-icon name="shield" [size]="14" /> Admin only</span>
                  <h2>System overview</h2>
                </div>
                <p class="panel-meta">{{ admin.completionPercentage }}% of all tasks complete</p>
              </div>
              <div class="admin-grid">
                <div>
                  <h3>Projects</h3>
                  <div class="status-grid">
                    <div class="status-chip total"><span>Total</span><strong>{{ admin.projects.totalProjects }}</strong></div>
                    @for (item of projectStatusRows(admin.projects); track item.label) {
                      <div [class]="'status-chip ' + item.tone"><span>{{ item.label }}</span><strong>{{ item.count }}</strong></div>
                    }
                  </div>
                </div>
                <div>
                  <h3>Tasks</h3>
                  <div class="status-grid">
                    <div class="status-chip total"><span>Total</span><strong>{{ admin.totalTasks }}</strong></div>
                    <div class="status-chip task-status-1"><span>To do</span><strong>{{ admin.toDoTasks }}</strong></div>
                    <div class="status-chip task-status-2"><span>In progress</span><strong>{{ admin.inProgressTasks }}</strong></div>
                    <div class="status-chip task-status-3"><span>Completed</span><strong>{{ admin.completedTasks }}</strong></div>
                    <div class="status-chip task-status-4"><span>Cancelled</span><strong>{{ admin.cancelledTasks }}</strong></div>
                    <div class="status-chip tone-danger"><span>Overdue</span><strong>{{ admin.overdueTasks }}</strong></div>
                  </div>
                </div>
                <div>
                  <h3>Users</h3>
                  <div class="status-grid">
                    <div class="status-chip total"><span>Total</span><strong>{{ admin.totalUsers }}</strong></div>
                    <div class="status-chip tone-success"><span>Active</span><strong>{{ admin.activeUsers }}</strong></div>
                    <div class="status-chip tone-muted"><span>Inactive</span><strong>{{ admin.inactiveUsers }}</strong></div>
                    <div class="status-chip tone-primary"><span>Admins</span><strong>{{ admin.adminUsers }}</strong></div>
                    <div class="status-chip tone-info"><span>Project managers</span><strong>{{ admin.projectManagerUsers }}</strong></div>
                    <div class="status-chip tone-neutral"><span>Team members</span><strong>{{ admin.teamMemberUsers }}</strong></div>
                  </div>
                </div>
              </div>
            </section>
          }
        </div>
      }

      <ng-template #taskRow let-task>
        <li class="task">
          <div class="task-main">
            <a class="task-title" [routerLink]="['/tasks', task.id]">{{ task.title }}</a>
            <div class="task-tags">
              <span [class]="'pill task-status-' + task.status">{{ statusLabel(task.status) }}</span>
              <span [class]="'priority-tag priority-' + task.priority">{{ priorityLabel(task.priority) }}</span>
            </div>
          </div>
          <span class="due" [attr.data-state]="due(task)">
            <app-icon [name]="due(task) === 'overdue' ? 'alert' : 'calendar'" [size]="15" />
            @if (due(task) === 'overdue') { <span class="sr-only">Overdue:</span> }
            <time>{{ task.dueDateUtc ? (task.dueDateUtc | utcDate:'mediumDate':'UTC') : 'No due date' }}</time>
          </span>
        </li>
      </ng-template>
    </main>
  `,
  styles: [`
    :host { display:block; }
    .loading { display:grid; gap:16px; }
    .kpi-skeleton { height:112px; border-radius:var(--radius-xl); }
    .block { height:260px; border-radius:var(--radius-xl); }
    .metrics { display:grid; grid-auto-flow:column; grid-auto-columns:minmax(0,1fr); margin-bottom:20px; overflow:hidden; border:1px solid var(--c-line); border-radius:var(--radius-xl); background:var(--c-surface); }
    .kpi { position:relative; display:grid; gap:2px; padding:18px 20px; box-shadow:1px 0 0 var(--c-line), 0 1px 0 var(--c-line); }
    .kpi-label { padding-right:40px; color:var(--c-muted); font-size:var(--text-sm); font-weight:500; }
    .kpi-value { margin-top:6px; color:var(--c-ink); font:700 var(--text-3xl)/1.1 var(--font-display); font-variant-numeric:tabular-nums; }
    .kpi.flagged .kpi-value { color:var(--c-danger-text); }
    .kpi-note { color:var(--c-muted); font-size:var(--text-xs); }
    .kpi-icon { position:absolute; top:16px; right:16px; display:flex; align-items:center; justify-content:center; width:32px; height:32px; border-radius:9px; background:var(--tone-bg); color:var(--tone-dot); }
    .dash-grid { display:grid; grid-template-columns:repeat(12,minmax(0,1fr)); gap:20px; }
    .span-5 { grid-column:span 5; } .span-6 { grid-column:span 6; } .span-7 { grid-column:span 7; } .span-12 { grid-column:1 / -1; }
    .panel-header { align-items:center; }
    .status-overview { display:grid; grid-template-columns:148px 1fr; align-items:center; gap:28px; }
    .ring { position:relative; margin:0; width:148px; height:148px; }
    .ring svg { width:100%; height:100%; transform:rotate(-90deg); }
    .ring circle { fill:none; stroke-width:14; }
    .ring-track { stroke:var(--c-neutral-soft); }
    .ring-segment { stroke:var(--tone-dot); }
    .ring figcaption { position:absolute; inset:0; display:grid; place-content:center; text-align:center; color:var(--c-muted); font-size:var(--text-xs); }
    .ring-value { color:var(--c-ink); font:700 var(--text-2xl)/1 var(--font-display); }
    .status-grid { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:10px; }
    .status-grid.wide { grid-template-columns:repeat(auto-fill,minmax(150px,1fr)); }
    .status-chip { display:grid; grid-template-columns:auto 1fr auto; align-items:center; gap:0 8px; padding:10px 12px; border:1px solid var(--c-line); border-radius:var(--radius-lg); }
    .status-chip::before { content:''; width:8px; height:8px; border-radius:50%; background:var(--tone-dot, transparent); }
    .status-chip span { color:var(--c-text-2); font-size:var(--text-sm); }
    .status-chip strong { font:650 var(--text-lg)/1.2 var(--font-display); font-variant-numeric:tabular-nums; }
    .status-chip small { grid-column:2 / -1; color:var(--c-faint); font-size:var(--text-xs); }
    .ribbon { display:flex; gap:3px; height:12px; margin-bottom:16px; overflow:hidden; border-radius:var(--radius-pill); background:var(--c-neutral-soft); }
    .ribbon-segment { background:var(--tone-dot); }
    .priority-grid { display:grid; gap:16px; padding-top:4px; }
    .priority-row { display:grid; grid-template-columns:84px 1fr 32px; align-items:center; gap:12px; font-size:var(--text-sm); }
    .priority-label { display:flex; align-items:center; gap:8px; color:var(--c-text-2); font-weight:500; }
    .priority-label::before { content:''; width:8px; height:8px; border-radius:2px; background:var(--tone-dot); }
    .priority-row strong { text-align:right; font-variant-numeric:tabular-nums; }
    .bar { height:10px; overflow:hidden; border-radius:var(--radius-pill); background:var(--c-neutral-soft); }
    .fill { height:100%; border-radius:inherit; background:var(--tone-dot); transition:width .4s ease; }
    .task-list { margin:0 -24px -20px; padding:0; list-style:none; }
    .task { display:flex; align-items:center; justify-content:space-between; gap:16px; padding:14px 24px; border-top:1px solid var(--c-line); }
    .task-main { display:grid; gap:8px; min-width:0; }
    .task-title { overflow:hidden; color:var(--c-text); font-weight:600; text-decoration:none; text-overflow:ellipsis; white-space:nowrap; }
    .task-title:hover { color:var(--c-primary-text); text-decoration:underline; }
    .task-tags { display:flex; flex-wrap:wrap; gap:6px; }
    .empty-state.compact { padding:28px 16px; }
    .empty-state a { font-size:var(--text-sm); font-weight:600; }
    .admin-overview { border-color:var(--c-ink); background:var(--c-ink); color:#c3c8dc; }
    .admin-overview h2, .admin-overview h3 { color:#fff; }
    .admin-overview .panel-meta { color:#a5abc4; }
    .admin-tag { display:inline-flex; align-items:center; gap:6px; margin-bottom:6px; color:var(--c-primary-on-ink); font-size:var(--text-xs); font-weight:600; }
    .admin-grid { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:28px; }
    .admin-grid h3 { margin-bottom:12px; font-size:var(--text-base); }
    .admin-overview .status-chip { border-color:rgb(255 255 255 / .1); background:rgb(255 255 255 / .04); }
    .admin-overview .status-chip span { color:#a5abc4; }
    .admin-overview .status-chip strong { color:#fff; }
    .admin-overview .status-chip.total { grid-template-columns:1fr auto; background:rgb(255 255 255 / .09); }
    .status-chip.total::before { display:none; }
    @media (max-width:1100px) {
      .metrics { grid-auto-flow:row; grid-template-columns:repeat(3,minmax(0,1fr)); }
      .span-5, .span-6, .span-7 { grid-column:1 / -1; }
      .admin-grid { grid-template-columns:1fr; }
    }
    @media (max-width:640px) {
      .page-header .btn { display:none; }
      .metrics { grid-template-columns:repeat(2,minmax(0,1fr)); }
      .kpi { padding:14px 16px; }
      .status-overview { grid-template-columns:1fr; justify-items:center; }
      .status-overview .status-grid { width:100%; }
      .task-list { margin:0 -16px -16px; }
      .task { flex-direction:column; align-items:flex-start; gap:8px; padding:12px 16px; }
      .task-main { width:100%; }
    }
  `]
})
export class DashboardComponent {
  private readonly dashboardService = inject(DashboardService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly session = inject(SessionStore);

  readonly summary = signal<DashboardSummary | null>(null);
  readonly statistics = signal<DashboardTaskStatistics | null>(null);
  readonly adminDashboard = signal<AdminDashboard | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');

  readonly today = new Date();
  readonly ringRadius = RING_RADIUS;

  readonly greeting = computed(() => {
    const hour = this.today.getHours();
    const salutation = hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening';
    const firstName = this.session.user()?.displayName?.trim().split(/\s+/)[0];
    return firstName ? `${salutation}, ${firstName}` : salutation;
  });

  /** Donut arcs for each non-empty task status, drawn clockwise from 12 o'clock. */
  readonly statusSegments = computed<RingSegment[]>(() => {
    const summary = this.summary();
    if (!summary) return [];
    const rows = this.taskStatusRows(summary).filter(row => row.count > 0);
    const total = rows.reduce((sum, row) => sum + row.count, 0);
    const gap = rows.length > 1 ? 2 : 0;
    let start = 0;
    return rows.map(row => {
      const length = (row.count / total) * RING_LENGTH;
      const segment = { tone: row.tone, dash: `${Math.max(length - gap, 0)} ${RING_LENGTH}`, offset: -start };
      start += length;
      return segment;
    });
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.dashboardService.getSummary().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: result => {
        this.summary.set(result);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load dashboard data. Please try again.');
        this.loading.set(false);
      }
    });

    // Priority and upcoming-task data are supplementary; the page still renders without them.
    this.dashboardService.getTaskStatistics().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: result => this.statistics.set(result),
      error: () => this.statistics.set(null)
    });

    // The API enforces the Admin role; the session check only avoids a pointless 403.
    if (this.session.isAdmin()) {
      this.dashboardService.getAdminDashboard().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: result => this.adminDashboard.set(result),
        error: () => this.adminDashboard.set(null)
      });
    } else {
      this.adminDashboard.set(null);
    }
  }

  pendingTasks(summary: DashboardSummary): number {
    return summary.toDoTasks + summary.inProgressTasks;
  }

  taskStatusRows(summary: DashboardSummary): StatusRow[] {
    return [
      { label: 'To do', count: summary.toDoTasks, tone: 'task-status-1' },
      { label: 'In progress', count: summary.inProgressTasks, tone: 'task-status-2' },
      { label: 'Completed', count: summary.completedTasks, tone: 'task-status-3' },
      { label: 'Cancelled', count: summary.cancelledTasks, tone: 'task-status-4' }
    ];
  }

  projectStatusRows(totals: DashboardProjectTotals): StatusRow[] {
    return [
      { label: 'Planning', count: totals.planningProjects, tone: 'project-status-1' },
      { label: 'Active', count: totals.activeProjects, tone: 'project-status-2' },
      { label: 'On hold', count: totals.onHoldProjects, tone: 'project-status-4' },
      { label: 'Completed', count: totals.completedProjects, tone: 'project-status-3' },
      { label: 'Archived', count: totals.archivedProjects, tone: 'project-status-5' }
    ];
  }

  priorityRows(stats: DashboardTaskStatistics): Array<{ label: string; count: number; priority: TaskPriority }> {
    return [
      { label: 'Critical', count: stats.criticalPriorityTasks, priority: TaskPriority.Critical },
      { label: 'High', count: stats.highPriorityTasks, priority: TaskPriority.High },
      { label: 'Medium', count: stats.mediumPriorityTasks, priority: TaskPriority.Medium },
      { label: 'Low', count: stats.lowPriorityTasks, priority: TaskPriority.Low }
    ];
  }

  barWidth(count: number, stats: DashboardTaskStatistics): number {
    const max = Math.max(
      stats.lowPriorityTasks,
      stats.mediumPriorityTasks,
      stats.highPriorityTasks,
      stats.criticalPriorityTasks
    );
    return max === 0 ? 0 : Math.round((count / max) * 100);
  }

  share(count: number, total: number): number {
    return total > 0 ? Math.round((count / total) * 100) : 0;
  }

  due(task: DashboardTaskItem): DueState {
    return dueState(task.dueDateUtc, task.status);
  }

  statusLabel(value: TaskStatus): string {
    return TASK_STATUS_LABELS[value] ?? 'Unknown';
  }

  priorityLabel(value: TaskPriority): string {
    return TASK_PRIORITY_LABELS[value] ?? 'Unknown';
  }
}
