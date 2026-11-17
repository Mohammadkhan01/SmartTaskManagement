import { API_BASE_URL } from '../../core/api/api.config';
import { DatePipe } from '@angular/common';
import { UtcDatePipe } from '../../shared/pipes/utc-date.pipe';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { SessionStore } from '../../core/auth/session-store';

interface DashboardTaskItem {
  id: string;
  projectId: string;
  title: string;
  status: number;
  priority: number;
  dueDateUtc: string | null;
}

interface DashboardProjectTotals {
  totalProjects: number;
  planningProjects: number;
  activeProjects: number;
  completedProjects: number;
  onHoldProjects: number;
  archivedProjects: number;
}

interface DashboardSummary {
  totalTasks: number;
  toDoTasks: number;
  inProgressTasks: number;
  completedTasks: number;
  cancelledTasks: number;
  overdueTasks: number;
  dueTodayTasks: number;
  completionPercentage: number;
  recentTasks: DashboardTaskItem[];
  projectTotals?: DashboardProjectTotals | null;
}

interface AdminDashboard {
  projects: DashboardProjectTotals;
  totalTasks: number;
  toDoTasks: number;
  inProgressTasks: number;
  completedTasks: number;
  cancelledTasks: number;
  overdueTasks: number;
  completionPercentage: number;
  totalUsers: number;
  activeUsers: number;
  inactiveUsers: number;
  adminUsers: number;
  projectManagerUsers: number;
  teamMemberUsers: number;
}

interface DashboardTaskStatistics {
  pendingTasks: number;
  lowPriorityTasks: number;
  mediumPriorityTasks: number;
  highPriorityTasks: number;
  criticalPriorityTasks: number;
  upcomingTasks: DashboardTaskItem[];
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  providers: [DatePipe],
  imports: [UtcDatePipe, RouterLink],
  template: `
    <main class="page">
      <header>
        <p class="eyebrow">SMART TASK MANAGEMENT</p>
        <h1>Dashboard</h1>
      </header>

      @if (loading()) {
        <p class="message">Loading your dashboard…</p>
      } @else if (error()) {
        <p class="error" role="alert">{{ error() }}</p>
        <button type="button" (click)="load()">Try again</button>
      } @else if (summary(); as data) {
        <section class="metrics" aria-label="Task summary">
          @if (data.projectTotals; as projects) {
            <article><span>Total projects</span><strong>{{ projects.totalProjects }}</strong></article>
          }
          <article><span>Total tasks</span><strong>{{ data.totalTasks }}</strong></article>
          <article><span>Completed</span><strong>{{ data.completedTasks }}</strong></article>
          <article><span>Pending</span><strong>{{ pendingTasks(data) }}</strong></article>
          <article><span>Overdue</span><strong>{{ data.overdueTasks }}</strong></article>
          <article><span>Due today</span><strong>{{ data.dueTodayTasks }}</strong></article>
        </section>

        <section class="panel" aria-label="Tasks by status">
          <div class="panel-heading">
            <div>
              <p class="eyebrow">PROGRESS</p>
              <h2>Tasks by status</h2>
            </div>
            <strong>{{ data.completedTasks }} completed vs {{ pendingTasks(data) }} pending</strong>
          </div>
          <div class="status-grid">
            @for (item of taskStatusRows(data); track item.label) {
              <div class="status-chip"><span>{{ item.label }}</span><strong>{{ item.count }}</strong></div>
            }
          </div>
        </section>

        @if (data.projectTotals; as projects) {
          <section class="panel" aria-label="Project summary">
            <div class="panel-heading">
              <div>
                <p class="eyebrow">YOUR PROJECTS</p>
                <h2>Projects by status</h2>
              </div>
              <strong>{{ projects.totalProjects }} total</strong>
            </div>
            <div class="status-grid">
              @for (item of projectStatusRows(projects); track item.label) {
                <div class="status-chip"><span>{{ item.label }}</span><strong>{{ item.count }}</strong></div>
              }
            </div>
          </section>
        }

        @if (statistics(); as stats) {
          <section class="panel" aria-label="Priority breakdown">
            <div class="panel-heading">
              <div>
                <p class="eyebrow">WORKLOAD</p>
                <h2>Tasks by priority</h2>
              </div>
              <strong>{{ stats.pendingTasks }} pending</strong>
            </div>
            <div class="priority-grid">
              @for (item of priorityRows(stats); track item.label) {
                <div class="priority-row">
                  <span class="priority-label">{{ item.label }}</span>
                  <div class="bar" role="presentation">
                    <div class="fill" [class]="'fill ' + item.css" [style.width.%]="barWidth(item.count, stats)"></div>
                  </div>
                  <strong>{{ item.count }}</strong>
                </div>
              }
            </div>
          </section>
        }

        <div class="columns">
          <section class="panel">
            <div class="panel-heading">
              <div>
                <p class="eyebrow">YOUR WORK</p>
                <h2>Recent tasks</h2>
              </div>
              <strong>{{ data.completionPercentage }}% complete</strong>
            </div>
            @if (data.recentTasks.length === 0) {
              <p class="message">No recent tasks found.</p>
            } @else {
              <div class="task-list">
                @for (task of data.recentTasks; track task.id) {
                  <article class="task">
                    <div>
                      <a [routerLink]="['/tasks', task.id]">{{ task.title }}</a>
                      <span>{{ statusLabel(task.status) }} · {{ priorityLabel(task.priority) }}</span>
                    </div>
                    <time>{{ task.dueDateUtc ? (task.dueDateUtc | utcDate:'mediumDate':'UTC') : 'No due date' }}</time>
                  </article>
                }
              </div>
            }
          </section>

          <section class="panel">
            <div class="panel-heading">
              <div>
                <p class="eyebrow">COMING UP</p>
                <h2>Upcoming tasks</h2>
              </div>
            </div>
            @if (!statistics()) {
              <p class="message">Upcoming tasks are unavailable right now.</p>
            } @else if (statistics()!.upcomingTasks.length === 0) {
              <p class="message">No upcoming due dates.</p>
            } @else {
              <div class="task-list">
                @for (task of statistics()!.upcomingTasks; track task.id) {
                  <article class="task">
                    <div>
                      <a [routerLink]="['/tasks', task.id]">{{ task.title }}</a>
                      <span>{{ statusLabel(task.status) }} · {{ priorityLabel(task.priority) }}</span>
                    </div>
                    <time>{{ task.dueDateUtc ? (task.dueDateUtc | utcDate:'mediumDate':'UTC') : 'No due date' }}</time>
                  </article>
                }
              </div>
            }
          </section>
        </div>

        @if (adminDashboard(); as admin) {
          <section class="panel" aria-label="System overview">
            <div class="panel-heading">
              <div>
                <p class="eyebrow">ADMIN</p>
                <h2>System overview</h2>
              </div>
              <strong>{{ admin.completionPercentage }}% of all tasks complete</strong>
            </div>
            <div class="admin-grid">
              <div>
                <h3>Projects</h3>
                <div class="status-grid">
                  <div class="status-chip"><span>Total</span><strong>{{ admin.projects.totalProjects }}</strong></div>
                  @for (item of projectStatusRows(admin.projects); track item.label) {
                    <div class="status-chip"><span>{{ item.label }}</span><strong>{{ item.count }}</strong></div>
                  }
                </div>
              </div>
              <div>
                <h3>Tasks</h3>
                <div class="status-grid">
                  <div class="status-chip"><span>Total</span><strong>{{ admin.totalTasks }}</strong></div>
                  <div class="status-chip"><span>To do</span><strong>{{ admin.toDoTasks }}</strong></div>
                  <div class="status-chip"><span>In progress</span><strong>{{ admin.inProgressTasks }}</strong></div>
                  <div class="status-chip"><span>Completed</span><strong>{{ admin.completedTasks }}</strong></div>
                  <div class="status-chip"><span>Cancelled</span><strong>{{ admin.cancelledTasks }}</strong></div>
                  <div class="status-chip"><span>Overdue</span><strong>{{ admin.overdueTasks }}</strong></div>
                </div>
              </div>
              <div>
                <h3>Users</h3>
                <div class="status-grid">
                  <div class="status-chip"><span>Total</span><strong>{{ admin.totalUsers }}</strong></div>
                  <div class="status-chip"><span>Active</span><strong>{{ admin.activeUsers }}</strong></div>
                  <div class="status-chip"><span>Inactive</span><strong>{{ admin.inactiveUsers }}</strong></div>
                  <div class="status-chip"><span>Admins</span><strong>{{ admin.adminUsers }}</strong></div>
                  <div class="status-chip"><span>Project managers</span><strong>{{ admin.projectManagerUsers }}</strong></div>
                  <div class="status-chip"><span>Team members</span><strong>{{ admin.teamMemberUsers }}</strong></div>
                </div>
              </div>
            </div>
          </section>
        }
      }
    </main>
  `,
  styles: [`
    :host { display:block; }
    .page { max-width:1180px; margin:auto; padding:28px 28px 48px; }
    .panel-heading { display:flex; align-items:center; justify-content:space-between; gap:20px; }
    h1 { margin:4px 0 24px; font-size:32px; }
    h2 { margin:4px 0 0; font-size:21px; }
    .eyebrow { margin:0; color:#64748b; font-size:11px; font-weight:700; letter-spacing:1.3px; }
    button { border:0; border-radius:8px; padding:11px 17px; background:#1d4ed8; color:white; cursor:pointer; }
    .metrics { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:16px; }
    .metrics article,.panel { background:white; border:1px solid #e5eaf1; border-radius:12px; padding:22px; }
    .metrics span,.task span { display:block; color:#64748b; font-size:13px; }
    .metrics strong { display:block; margin-top:12px; font-size:30px; }
    .panel { margin-top:22px; }
    .columns { display:grid; grid-template-columns:1fr 1fr; gap:22px; }
    .priority-grid { display:grid; gap:12px; margin-top:18px; }
    .priority-row { display:grid; grid-template-columns:80px 1fr 40px; align-items:center; gap:14px; font-size:13px; }
    .priority-label { color:#475569; }
    .priority-row strong { text-align:right; }
    .bar { height:10px; border-radius:6px; background:#eef2f7; overflow:hidden; }
    .fill { height:100%; border-radius:6px; background:#94a3b8; }
    .fill.medium { background:#60a5fa; }
    .fill.high { background:#f97316; }
    .fill.critical { background:#dc2626; }
    .status-grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(130px,1fr)); gap:12px; margin-top:16px; }
    .status-chip { border:1px solid #edf0f4; border-radius:10px; padding:12px 14px; }
    .status-chip span { display:block; color:#64748b; font-size:12px; }
    .status-chip strong { display:block; margin-top:6px; font-size:20px; }
    .admin-grid { display:grid; gap:22px; margin-top:8px; }
    h3 { margin:12px 0 0; font-size:15px; color:#334155; }
    .task-list { margin-top:18px; }
    .task { display:flex; justify-content:space-between; align-items:center; gap:16px; padding:17px 0; border-top:1px solid #edf0f4; }
    .task a { color:#17243b; font-weight:600; text-decoration:none; }
    .task a:hover { color:#1d4ed8; }
    .task span { margin-top:6px; }
    time { color:#64748b; font-size:13px; white-space:nowrap; }
    .message { color:#64748b; padding:20px 0; }
    .error { color:#b91c1c; }
    @media(max-width:900px) { .columns { grid-template-columns:1fr; } }
    @media(max-width:650px) { .page {padding:20px 16px;} .metrics {grid-template-columns:repeat(2,minmax(0,1fr));} .panel-heading {align-items:flex-start; flex-direction:column;} .task {align-items:flex-start; flex-direction:column;} }
  `]
})
export class DashboardComponent {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionStore);

  readonly summary = signal<DashboardSummary | null>(null);
  readonly statistics = signal<DashboardTaskStatistics | null>(null);
  readonly adminDashboard = signal<AdminDashboard | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.http.get<DashboardSummary>(`${API_BASE_URL}/dashboard/summary`).subscribe({
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
    this.http.get<DashboardTaskStatistics>(`${API_BASE_URL}/dashboard/task-statistics`).subscribe({
      next: result => this.statistics.set(result),
      error: () => this.statistics.set(null)
    });

    // The API enforces the Admin role; the session check only avoids a pointless 403.
    if (this.session.isAdmin()) {
      this.http.get<AdminDashboard>(`${API_BASE_URL}/admin/dashboard`).subscribe({
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

  taskStatusRows(summary: DashboardSummary): Array<{ label: string; count: number }> {
    return [
      { label: 'To do', count: summary.toDoTasks },
      { label: 'In progress', count: summary.inProgressTasks },
      { label: 'Completed', count: summary.completedTasks },
      { label: 'Cancelled', count: summary.cancelledTasks }
    ];
  }

  projectStatusRows(totals: DashboardProjectTotals): Array<{ label: string; count: number }> {
    return [
      { label: 'Planning', count: totals.planningProjects },
      { label: 'Active', count: totals.activeProjects },
      { label: 'On hold', count: totals.onHoldProjects },
      { label: 'Completed', count: totals.completedProjects },
      { label: 'Archived', count: totals.archivedProjects }
    ];
  }

  priorityRows(stats: DashboardTaskStatistics): Array<{ label: string; count: number; css: string }> {
    return [
      { label: 'Critical', count: stats.criticalPriorityTasks, css: 'critical' },
      { label: 'High', count: stats.highPriorityTasks, css: 'high' },
      { label: 'Medium', count: stats.mediumPriorityTasks, css: 'medium' },
      { label: 'Low', count: stats.lowPriorityTasks, css: 'low' }
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

  statusLabel(value: number): string {
    return ({ 1: 'To do', 2: 'In progress', 3: 'Completed', 4: 'Cancelled' } as Record<number, string>)[value] ?? 'Unknown';
  }

  priorityLabel(value: number): string {
    return ({ 1: 'Low', 2: 'Medium', 3: 'High', 4: 'Critical' } as Record<number, string>)[value] ?? 'Unknown';
  }
}
