import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { API_BASE_URL } from '../../core/api/api.config';
import { SessionStore } from '../../core/auth/session-store';
import { DashboardComponent } from './dashboard.component';

const SUMMARY = `${API_BASE_URL}/dashboard/summary`;
const STATISTICS = `${API_BASE_URL}/dashboard/task-statistics`;
const ADMIN = `${API_BASE_URL}/admin/dashboard`;

const summary = {
  totalTasks: 12,
  toDoTasks: 5,
  inProgressTasks: 4,
  completedTasks: 3,
  cancelledTasks: 0,
  overdueTasks: 2,
  dueTodayTasks: 1,
  completionPercentage: 25,
  projectTotals: {
    totalProjects: 7,
    planningProjects: 1,
    activeProjects: 3,
    completedProjects: 2,
    onHoldProjects: 1,
    archivedProjects: 0
  },
  recentTasks: [
    { id: 't1', projectId: 'p1', title: 'Fix login', status: 2, priority: 4, dueDateUtc: '2026-03-04T12:00:00' },
    { id: 't2', projectId: 'p1', title: 'Write docs', status: 1, priority: 1, dueDateUtc: null }
  ]
};

const statistics = {
  pendingTasks: 9,
  lowPriorityTasks: 2,
  mediumPriorityTasks: 4,
  highPriorityTasks: 1,
  criticalPriorityTasks: 0,
  upcomingTasks: [
    { id: 't3', projectId: 'p2', title: 'Release v2', status: 1, priority: 3, dueDateUtc: '2026-04-01T12:00:00Z' }
  ]
};

const adminDashboard = {
  projects: {
    totalProjects: 20,
    planningProjects: 4,
    activeProjects: 9,
    completedProjects: 5,
    onHoldProjects: 1,
    archivedProjects: 1
  },
  totalTasks: 140,
  toDoTasks: 50,
  inProgressTasks: 40,
  completedTasks: 35,
  cancelledTasks: 15,
  overdueTasks: 6,
  completionPercentage: 25,
  totalUsers: 31,
  activeUsers: 29,
  inactiveUsers: 2,
  adminUsers: 1,
  projectManagerUsers: 5,
  teamMemberUsers: 25
};

describe('DashboardComponent', () => {
  let fixture: ComponentFixture<DashboardComponent>;
  let element: HTMLElement;
  let http: HttpTestingController;

  const render = () => fixture.detectChanges();
  const metric = (label: string) =>
    Array.from(element.querySelectorAll('.metrics article'))
      .find(a => a.querySelector('span')?.textContent?.trim() === label)
      ?.querySelector('strong')?.textContent?.trim();
  const panel = (heading: string) =>
    Array.from(element.querySelectorAll('section.panel'))
      .find(s => s.querySelector('h2')?.textContent?.trim() === heading)!;

  const chips = (container: Element) =>
    Object.fromEntries(Array.from(container.querySelectorAll('.status-chip')).map(chip => [
      chip.querySelector('span')?.textContent?.trim(),
      chip.querySelector('strong')?.textContent?.trim()
    ]));

  function create(roles: string[] = ['Team Member']): void {
    TestBed.inject(SessionStore).setSession('token', {
      userId: 'u1',
      displayName: 'User',
      email: 'user@example.com',
      roles
    });
    fixture = TestBed.createComponent(DashboardComponent);
    element = fixture.nativeElement;
    render();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function flush(summaryBody: object = summary, statisticsBody: object | 'error' = statistics): void {
    http.expectOne(SUMMARY).flush(summaryBody);
    const stats = http.expectOne(STATISTICS);
    if (statisticsBody === 'error') {
      stats.flush(null, { status: 500, statusText: 'Error' });
    } else {
      stats.flush(statisticsBody);
    }
    render();
  }

  it('shows a loading state until the summary arrives', () => {
    create();
    expect(element.querySelector('.message')?.textContent).toContain('Loading your dashboard');
    flush();
    expect(element.textContent).not.toContain('Loading your dashboard');
  });

  it('renders the task summary metrics', () => {
    create();
    flush();

    expect(metric('Total projects')).toBe('7');
    expect(metric('Total tasks')).toBe('12');
    expect(metric('Completed')).toBe('3');
    expect(metric('Pending')).toBe('9');
    expect(metric('Overdue')).toBe('2');
    expect(metric('Due today')).toBe('1');
  });

  it('renders tasks by status with completed versus pending', () => {
    create();
    flush({ ...summary, totalTasks: 14, cancelledTasks: 2 });

    const byStatus = panel('Tasks by status');
    expect(byStatus.textContent).toContain('3 completed vs 9 pending');
    expect(chips(byStatus)).toEqual({
      'To do': '5',
      'In progress': '4',
      Completed: '3',
      Cancelled: '2'
    });
  });

  it('renders the priority breakdown scaled to the largest bucket', () => {
    create();
    flush();

    const breakdown = element.querySelector('[aria-label="Priority breakdown"]')!;
    expect(breakdown.textContent).toContain('9 pending');
    const rows = Array.from(breakdown.querySelectorAll('.priority-row')).map(row => ({
      label: row.querySelector('.priority-label')?.textContent?.trim(),
      count: row.querySelector('strong')?.textContent?.trim(),
      width: (row.querySelector('.fill') as HTMLElement).style.width
    }));
    expect(rows).toEqual([
      { label: 'Critical', count: '0', width: '0%' },
      { label: 'High', count: '1', width: '25%' },
      { label: 'Medium', count: '4', width: '100%' },
      { label: 'Low', count: '2', width: '50%' }
    ]);
  });

  it('renders recent tasks with labels, links and due dates', () => {
    create();
    flush();

    const recent = panel('Recent tasks');
    expect(recent.textContent).toContain('25% complete');
    const tasks = recent.querySelectorAll('.task');
    expect(tasks).toHaveLength(2);
    expect(tasks[0].textContent).toContain('In progress · Critical');
    expect(tasks[0].querySelector('time')?.textContent).toContain('Mar 4, 2026');
    expect(tasks[0].querySelector('a')?.getAttribute('href')).toBe('/tasks/t1');
    expect(tasks[1].textContent).toContain('To do · Low');
    expect(tasks[1].querySelector('time')?.textContent).toContain('No due date');
  });

  it('renders upcoming tasks', () => {
    create();
    flush();

    const upcoming = panel('Upcoming tasks');
    const tasks = upcoming.querySelectorAll('.task');
    expect(tasks).toHaveLength(1);
    expect(tasks[0].textContent).toContain('Release v2');
    expect(tasks[0].textContent).toContain('To do · High');
    expect(tasks[0].querySelector('time')?.textContent).toContain('Apr 1, 2026');
    expect(tasks[0].querySelector('a')?.getAttribute('href')).toBe('/tasks/t3');
  });

  it('shows empty states for recent and upcoming tasks', () => {
    create();
    flush({ ...summary, recentTasks: [] }, { ...statistics, upcomingTasks: [] });

    expect(panel('Recent tasks').textContent).toContain('No recent tasks found.');
    expect(panel('Upcoming tasks').textContent).toContain('No upcoming due dates.');
  });

  it('still renders the summary when statistics fail', () => {
    create();
    flush(summary, 'error');

    expect(metric('Total tasks')).toBe('12');
    expect(element.querySelector('[aria-label="Priority breakdown"]')).toBeNull();
    expect(panel('Upcoming tasks').textContent).toContain('Upcoming tasks are unavailable right now.');
  });

  it('shows an error with retry when the summary fails', () => {
    create();
    http.expectOne(SUMMARY).flush(null, { status: 500, statusText: 'Error' });
    http.expectOne(STATISTICS).flush(statistics);
    render();

    expect(element.querySelector('[role="alert"]')?.textContent)
      .toContain('Could not load dashboard data. Please try again.');

    element.querySelector<HTMLButtonElement>('button')!.click();
    render();
    expect(element.querySelector('.message')?.textContent).toContain('Loading your dashboard');
    flush();
    expect(metric('Total tasks')).toBe('12');
  });

  it('renders project totals by status from the summary', () => {
    create();
    flush();

    const projects = panel('Projects by status');
    expect(projects.textContent).toContain('7 total');
    expect(chips(projects)).toEqual({
      Planning: '1',
      Active: '3',
      Completed: '2',
      'On hold': '1',
      Archived: '0'
    });
  });

  it('omits the project panel when the summary has no project totals', () => {
    create();
    flush({ ...summary, projectTotals: null });

    expect(element.querySelector('[aria-label="Project summary"]')).toBeNull();
    expect(metric('Total projects')).toBeUndefined();
    expect(metric('Total tasks')).toBe('12');
  });

  it('does not call the admin endpoint or show the system overview for non-admin roles', () => {
    for (const roles of [['Team Member'], ['Project Manager']]) {
      create(roles);
      flush();

      http.expectNone(ADMIN);
      expect(element.querySelector('[aria-label="System overview"]')).toBeNull();
      fixture.destroy();
    }
  });

  it('loads and renders the system overview for admins', () => {
    create(['Admin']);
    http.expectOne(ADMIN).flush(adminDashboard);
    flush();

    const overview = element.querySelector('[aria-label="System overview"]')!;
    expect(overview).not.toBeNull();
    expect(overview.textContent).toContain('25% of all tasks complete');

    const [projects, tasks, users] = Array.from(overview.querySelectorAll('.admin-grid > div'));
    expect(projects.querySelector('h3')?.textContent).toBe('Projects');
    expect(chips(projects)).toEqual({
      Total: '20', Planning: '4', Active: '9', Completed: '5', 'On hold': '1', Archived: '1'
    });
    expect(chips(tasks)).toEqual({
      Total: '140', 'To do': '50', 'In progress': '40', Completed: '35', Cancelled: '15', Overdue: '6'
    });
    expect(chips(users)).toEqual({
      Total: '31', Active: '29', Inactive: '2', Admins: '1', 'Project managers': '5', 'Team members': '25'
    });
  });

  it('keeps the personal dashboard when the admin overview fails', () => {
    create(['Admin']);
    http.expectOne(ADMIN).flush(null, { status: 500, statusText: 'Error' });
    flush();

    expect(metric('Total tasks')).toBe('12');
    expect(element.querySelector('[aria-label="System overview"]')).toBeNull();
  });
});
