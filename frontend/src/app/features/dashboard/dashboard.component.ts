import { API_BASE_URL } from '../../core/api/api.config';
import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../../core/auth/auth.service';

interface DashboardSummary {
  totalTasks: number;
  toDoTasks: number;
  inProgressTasks: number;
  completedTasks: number;
  overdueTasks: number;
  dueTodayTasks: number;
  completionPercentage: number;
  recentTasks: Array<{
    id: string;
    title: string;
    status: number;
    priority: number;
    dueDateUtc: string | null;
  }>;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [DatePipe],
  template: `
    <main class="page">
      <header class="topbar">
        <div>
          <p class="eyebrow">SMART TASK MANAGEMENT</p>
          <h1>Dashboard</h1>
        </div>
        <button type="button" (click)="logout()">Sign out</button>
      </header>

      @if (loading()) {
        <p class="message">Loading your dashboard…</p>
      } @else if (error()) {
        <p class="error" role="alert">{{ error() }}</p>
        <button type="button" (click)="load()">Try again</button>
      } @else if (summary(); as data) {
        <section class="metrics" aria-label="Task summary">
          <article><span>Total tasks</span><strong>{{ data.totalTasks }}</strong></article>
          <article><span>To do</span><strong>{{ data.toDoTasks }}</strong></article>
          <article><span>In progress</span><strong>{{ data.inProgressTasks }}</strong></article>
          <article><span>Completed</span><strong>{{ data.completedTasks }}</strong></article>
          <article><span>Overdue</span><strong>{{ data.overdueTasks }}</strong></article>
          <article><span>Due today</span><strong>{{ data.dueTodayTasks }}</strong></article>
        </section>

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
                    <strong>{{ task.title }}</strong>
                    <span>{{ statusLabel(task.status) }} · {{ priorityLabel(task.priority) }}</span>
                  </div>
                  <time>{{ task.dueDateUtc ? (task.dueDateUtc | date:'mediumDate') : 'No due date' }}</time>
                </article>
              }
            </div>
          }
        </section>
      }
    </main>
  `,
  styles: [`
    :host { display:block; min-height:100vh; background:#f4f6fa; }
    .page { max-width:1180px; margin:auto; padding:36px 28px; }
    .topbar,.panel-heading { display:flex; align-items:center; justify-content:space-between; gap:20px; }
    h1 { margin:4px 0 28px; font-size:32px; }
    h2 { margin:4px 0 0; font-size:21px; }
    .eyebrow { margin:0; color:#64748b; font-size:11px; font-weight:700; letter-spacing:1.3px; }
    button { border:0; border-radius:8px; padding:11px 17px; background:#1d4ed8; color:white; cursor:pointer; }
    .metrics { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:16px; }
    .metrics article,.panel { background:white; border:1px solid #e5eaf1; border-radius:12px; padding:22px; }
    .metrics span,.task span { display:block; color:#64748b; font-size:13px; }
    .metrics strong { display:block; margin-top:12px; font-size:30px; }
    .panel { margin-top:22px; }
    .task-list { margin-top:18px; }
    .task { display:flex; justify-content:space-between; align-items:center; gap:16px; padding:17px 0; border-top:1px solid #edf0f4; }
    .task span { margin-top:6px; }
    time { color:#64748b; font-size:13px; white-space:nowrap; }
    .message { color:#64748b; padding:20px 0; }
    .error { color:#b91c1c; }
    @media(max-width:650px) { .page {padding:24px 16px;} .metrics {grid-template-columns:repeat(2,minmax(0,1fr));} .panel-heading {align-items:flex-start; flex-direction:column;} .task {align-items:flex-start; flex-direction:column;} }
  `]
})
export class DashboardComponent {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly summary = signal<DashboardSummary | null>(null);
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
  }

  async logout(): Promise<void> {
    try {
      await this.auth.logout();
    } finally {
      await this.router.navigateByUrl('/login');
    }
  }

  statusLabel(value: number): string {
    return ({ 1: 'To do', 2: 'In progress', 3: 'Completed', 4: 'Cancelled' } as Record<number, string>)[value] ?? 'Unknown';
  }

  priorityLabel(value: number): string {
    return ({ 1: 'Low', 2: 'Medium', 3: 'High', 4: 'Critical' } as Record<number, string>)[value] ?? 'Unknown';
  }
}
