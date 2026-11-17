import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { API_BASE_URL } from '../../core/api/api.config';
import { SessionStore } from '../../core/auth/session-store';
import { ProjectMemberRole, ProjectStatus } from '../../core/projects/project.models';
import { Task, TaskListResponse, TaskPriority, TaskStatus } from '../../core/tasks/task.models';
import { TaskListComponent } from './task-list.component';

const TASKS = `${API_BASE_URL}/projects/p1/tasks`;

const task = (id: string, overrides: Partial<Task> = {}): Task => ({
  id,
  projectId: 'p1',
  title: `Task ${id}`,
  description: null,
  status: TaskStatus.ToDo,
  priority: TaskPriority.Medium,
  assignedToUserId: null,
  createdByUserId: 'u1',
  dueDateUtc: null,
  createdAtUtc: '2026-01-01T00:00:00',
  updatedAtUtc: null,
  rowVersion: 'rv',
  ...overrides
});

const page = (items: Task[], overrides: Partial<TaskListResponse> = {}): TaskListResponse => ({
  items,
  pageNumber: 1,
  pageSize: 20,
  totalCount: items.length,
  totalPages: 1,
  hasPreviousPage: false,
  hasNextPage: false,
  ...overrides
});

describe('TaskListComponent', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;
  let element: HTMLElement;

  const expectTasks = (): TestRequest => http.expectOne(r => r.method === 'GET' && r.url === TASKS);
  const render = () => harness.detectChanges();
  const button = (label: string) =>
    Array.from(element.querySelectorAll('button')).find(b => b.textContent?.trim() === label)!;

  async function open(roles: string[] = ['Project Manager'], firstPage = page([task('a')])): Promise<void> {
    TestBed.inject(SessionStore).setSession('t', { userId: 'me', displayName: 'Pat', email: 'p@x', roles });
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/projects/p1/tasks', TaskListComponent);
    element = harness.routeNativeElement!;

    http.expectOne(`${API_BASE_URL}/projects/p1`).flush({
      id: 'p1', name: 'Apollo', description: null, status: ProjectStatus.Planning,
      createdByUserId: 'u1', createdAtUtc: '2026-01-01T00:00:00', rowVersion: 'rv'
    });
    http.expectOne(`${API_BASE_URL}/projects/p1/members`).flush([
      { userId: 'u2', displayName: 'Sam', email: 's@x', projectRole: ProjectMemberRole.Member }
    ]);
    expectTasks().flush(firstPage);
    render();
  }

  function select(label: string, value: string): void {
    const el = Array.from(element.querySelectorAll('.filters label'))
      .find(l => l.querySelector('span')?.textContent === label)!
      .querySelector('select')!;
    el.value = value;
    el.dispatchEvent(new Event('change'));
    render();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'projects/:id/tasks', component: TaskListComponent }]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    http.verify();
  });

  it('requests the first page with default sort and renders rows', async () => {
    TestBed.inject(SessionStore).setSession('t', { userId: 'me', displayName: 'Pat', email: 'p@x', roles: [] });
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/projects/p1/tasks', TaskListComponent);
    element = harness.routeNativeElement!;
    expect(element.querySelector('[role="status"]')?.textContent).toContain('Loading tasks');

    http.expectOne(`${API_BASE_URL}/projects/p1`).flush(null, { status: 404, statusText: 'Not Found' });
    http.expectOne(`${API_BASE_URL}/projects/p1/members`).flush(null, { status: 403, statusText: 'Forbidden' });
    const req = expectTasks();
    expect(req.request.params.get('pageNumber')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('20');
    expect(req.request.params.get('sortBy')).toBe('createdAt');
    expect(req.request.params.get('sortDescending')).toBe('true');
    req.flush(page([
      task('a', { assignedToUserId: 'me', priority: TaskPriority.Critical, dueDateUtc: '2026-02-03T12:00:00Z' }),
      task('b', { assignedToUserId: 'u2', status: TaskStatus.InProgress })
    ]));
    render();

    const rows = element.querySelectorAll('tbody tr');
    expect(rows).toHaveLength(2);
    expect(rows[0].textContent).toContain('Critical');
    expect(rows[0].textContent).toContain('You');
    expect(rows[0].textContent).toContain('Feb 3, 2026');
    expect(rows[1].textContent).toContain('In progress');
    expect(rows[1].textContent).toContain('Project member');
  });

  it('shows project context, member names and manager actions', async () => {
    await open(['Project Manager'], page([task('a', { assignedToUserId: 'u2' })]));

    expect(element.querySelector('h1')?.textContent).toContain('Apollo');
    expect(element.querySelector('.subtitle')?.textContent).toContain('Tasks can only be assigned once the project is Active');
    expect(element.querySelector('tbody tr')?.textContent).toContain('Sam');
    expect(element.querySelector('a[href="/projects/p1/tasks/new"]')).not.toBeNull();
    expect(element.querySelector('a[href="/tasks/a/edit"]')).not.toBeNull();
  });

  it('hides create/edit/delete for team members', async () => {
    await open(['Team Member']);
    expect(element.querySelector('a[href="/projects/p1/tasks/new"]')).toBeNull();
    expect(element.querySelector('a[href="/tasks/a/edit"]')).toBeNull();
    expect(button('Delete')).toBeUndefined();
  });

  it('sends status, priority, sort and order filters and resets to page 1', async () => {
    await open(['Team Member'], page([task('a')], { hasNextPage: true, totalPages: 2 }));
    button('Next').click();
    expect(expectTasks().request.params.get('pageNumber')).toBe('2');

    select('Status', String(TaskStatus.Completed));
    let req = expectTasks();
    expect(req.request.params.get('status')).toBe('3');
    expect(req.request.params.get('pageNumber')).toBe('1');
    req.flush(page([]));

    select('Priority', String(TaskPriority.High));
    req = expectTasks();
    expect(req.request.params.get('status')).toBe('3');
    expect(req.request.params.get('priority')).toBe('3');
    req.flush(page([]));
    render();
    expect(element.querySelector('.empty')?.textContent).toContain('Try changing your search or filters.');

    select('Sort by', 'dueDate');
    req = expectTasks();
    expect(req.request.params.get('sortBy')).toBe('dueDate');
    req.flush(page([]));

    select('Order', 'asc');
    req = expectTasks();
    expect(req.request.params.get('sortDescending')).toBe('false');
    expect(req.request.params.get('sortBy')).toBe('dueDate');
    req.flush(page([]));

    select('Status', '');
    req = expectTasks();
    expect(req.request.params.has('status')).toBe(false);
    req.flush(page([]));
  });

  it('debounces search', async () => {
    await open();
    vi.useFakeTimers();
    const input = element.querySelector<HTMLInputElement>('input[type="search"]')!;
    input.value = 'lo';
    input.dispatchEvent(new Event('input'));
    vi.advanceTimersByTime(200);
    input.value = 'login';
    input.dispatchEvent(new Event('input'));
    vi.advanceTimersByTime(349);
    http.expectNone(TASKS);

    vi.advanceTimersByTime(1);
    const req = expectTasks();
    expect(req.request.params.get('search')).toBe('login');
    expect(req.request.params.get('pageNumber')).toBe('1');
    req.flush(page([]));
  });

  it('shows the empty project message when there are no tasks and no filters', async () => {
    await open(['Team Member'], page([]));
    expect(element.querySelector('.empty')?.textContent).toContain('This project has no tasks yet.');
  });

  it('shows a load error with retry', async () => {
    TestBed.inject(SessionStore).setSession('t', { userId: 'me', displayName: 'Pat', email: 'p@x', roles: [] });
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/projects/p1/tasks', TaskListComponent);
    element = harness.routeNativeElement!;
    http.expectOne(`${API_BASE_URL}/projects/p1`).flush(null, { status: 404, statusText: 'Not Found' });
    http.expectOne(`${API_BASE_URL}/projects/p1/members`).flush([]);
    expectTasks().flush(null, { status: 404, statusText: 'Not Found' });
    render();

    expect(element.querySelector('[role="alert"]')?.textContent)
      .toContain('Project not found or you do not have access to its tasks.');
    button('Try again').click();
    expectTasks().flush(page([task('a')]));
    render();
    expect(element.querySelectorAll('tbody tr')).toHaveLength(1);
  });

  it('deletes a task after confirmation and reloads', async () => {
    await open();
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    button('Delete').click();
    http.expectOne(r => r.method === 'DELETE' && r.url === `${API_BASE_URL}/tasks/a`).flush(null);
    expectTasks().flush(page([]));
  });

  it('reports delete failures', async () => {
    await open();
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    button('Delete').click();
    http.expectOne(r => r.method === 'DELETE').flush(null, { status: 403, statusText: 'Forbidden' });
    render();
    expect(element.querySelector('[role="alert"]')?.textContent)
      .toContain('You do not have permission to delete this task.');
  });
});
