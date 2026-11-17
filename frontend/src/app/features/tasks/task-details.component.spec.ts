import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { API_BASE_URL } from '../../core/api/api.config';
import { SessionStore } from '../../core/auth/session-store';
import { Project, ProjectMemberRole, ProjectStatus } from '../../core/projects/project.models';
import { Task, TaskPriority, TaskStatus } from '../../core/tasks/task.models';
import { TaskDetailsComponent } from './task-details.component';

const TASK = `${API_BASE_URL}/tasks/t1`;

const baseTask: Task = {
  id: 't1',
  projectId: 'p1',
  title: 'Fix login',
  description: 'Login is broken.',
  status: TaskStatus.ToDo,
  priority: TaskPriority.High,
  assignedToUserId: null,
  createdByUserId: 'u1',
  dueDateUtc: '2026-05-06T12:00:00',
  createdAtUtc: '2026-01-01T00:00:00',
  updatedAtUtc: null,
  rowVersion: 'rv-1'
};

const activeProject: Project = {
  id: 'p1',
  name: 'Apollo',
  description: null,
  status: ProjectStatus.Active,
  createdByUserId: 'u1',
  createdAtUtc: '2026-01-01T00:00:00',
  rowVersion: 'rv'
};

const members = [
  { userId: 'me', displayName: 'Pat', email: 'pat@x', projectRole: ProjectMemberRole.Manager },
  { userId: 'u2', displayName: 'Sam', email: 'sam@x', projectRole: ProjectMemberRole.Member }
];

describe('TaskDetailsComponent', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;
  let element: HTMLElement;

  const render = () => harness.detectChanges();
  const button = (label: string) =>
    Array.from(element.querySelectorAll('button')).find(b => b.textContent?.trim() === label)!;

  async function open(options: { roles?: string[]; task?: Partial<Task>; project?: Partial<Project> } = {}) {
    TestBed.inject(SessionStore).setSession('t', {
      userId: 'me',
      displayName: 'Pat',
      email: 'pat@x',
      roles: options.roles ?? ['Project Manager']
    });
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/tasks/t1', TaskDetailsComponent);
    element = harness.routeNativeElement!;
    http.expectOne(TASK).flush({ ...baseTask, ...options.task });
    http.expectOne(`${API_BASE_URL}/projects/p1`).flush({ ...activeProject, ...options.project });
    http.expectOne(`${API_BASE_URL}/projects/p1/members`).flush(members);
    render();
  }

  function choose(selector: string, value: string): void {
    const select = element.querySelector<HTMLSelectElement>(selector)!;
    select.value = value;
    select.dispatchEvent(new Event('change'));
    render();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'tasks/:taskId', component: TaskDetailsComponent }]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    http.verify();
  });

  it('renders task details with project context', async () => {
    await open();

    expect(element.querySelector('h1')?.textContent).toContain('Fix login');
    expect(element.querySelector('.eyebrow')?.textContent).toContain('Apollo');
    expect(element.textContent).toContain('To do');
    expect(element.textContent).toContain('High priority');
    expect(element.textContent).toContain('Unassigned');
    expect(element.textContent).toContain('May 6, 2026');
    expect(element.querySelector('a[href="/tasks/t1/edit"]')).not.toBeNull();
  });

  it('shows a not-found error with retry', async () => {
    TestBed.inject(SessionStore).setSession('t', { userId: 'me', displayName: 'Pat', email: 'p@x', roles: [] });
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/tasks/t1', TaskDetailsComponent);
    element = harness.routeNativeElement!;
    expect(element.querySelector('[role="status"]')?.textContent).toContain('Loading task');

    http.expectOne(TASK).flush(null, { status: 404, statusText: 'Not Found' });
    render();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Task not found');

    button('Try again').click();
    http.expectOne(TASK).flush(baseTask);
    http.expectOne(`${API_BASE_URL}/projects/p1`).flush(activeProject);
    http.expectOne(`${API_BASE_URL}/projects/p1/members`).flush([]);
    render();
    expect(element.querySelector('h1')?.textContent).toContain('Fix login');
  });

  describe('status', () => {
    it('updates the status with the row version', async () => {
      await open();
      const update = () => button('Update status');
      expect(update().disabled).toBe(true);

      choose('#task-status', String(TaskStatus.InProgress));
      expect(update().disabled).toBe(false);
      update().click();

      const put = http.expectOne(`${TASK}/status`);
      expect(put.request.method).toBe('PUT');
      expect(put.request.body).toEqual({ status: TaskStatus.InProgress, rowVersion: 'rv-1' });
      put.flush({ ...baseTask, status: TaskStatus.InProgress, rowVersion: 'rv-2' });
      render();

      expect(element.querySelector('.success')?.textContent).toContain('Status changed to In progress.');
      expect(update().disabled).toBe(true);
    });

    it('shows the conflict message on 409', async () => {
      await open();
      choose('#task-status', String(TaskStatus.Completed));
      button('Update status').click();
      http.expectOne(`${TASK}/status`).flush(null, { status: 409, statusText: 'Conflict' });
      render();

      expect(element.querySelector('[role="alert"]')?.textContent)
        .toContain('This task was changed by someone else. Reload the page and try again.');
    });

    it('lets the assignee change status even without a manager role', async () => {
      await open({ roles: ['Team Member'], task: { assignedToUserId: 'me' } });
      expect(element.querySelector('#task-status')).not.toBeNull();
      expect(element.textContent).toContain('Pat (you)');
      expect(element.textContent).toContain('Only admins and project managers can assign tasks.');
    });

    it('blocks status changes for other team members', async () => {
      await open({ roles: ['Team Member'], task: { assignedToUserId: 'u2' } });
      expect(element.querySelector('#task-status')).toBeNull();
      expect(element.textContent).toContain("Only the assignee or a project manager can change this task's status.");
      expect(element.querySelector('a[href="/tasks/t1/edit"]')).toBeNull();
    });
  });

  describe('assignment', () => {
    it('assigns the selected member with the row version', async () => {
      await open();
      const assign = () => button('Assign');
      expect(assign().disabled).toBe(true);

      choose('#task-assignee', 'u2');
      assign().click();

      const put = http.expectOne(`${TASK}/assign`);
      expect(put.request.method).toBe('PUT');
      expect(put.request.body).toEqual({ assignedToUserId: 'u2', rowVersion: 'rv-1' });
      put.flush({ ...baseTask, assignedToUserId: 'u2', rowVersion: 'rv-2' });
      render();

      expect(element.querySelector('.success')?.textContent).toContain('Task assigned to Sam.');
      expect(assign().disabled).toBe(true);
    });

    it('reports a rejected assignment', async () => {
      await open();
      choose('#task-assignee', 'u2');
      button('Assign').click();
      http.expectOne(`${TASK}/assign`).flush(null, { status: 400, statusText: 'Bad Request' });
      render();

      expect(element.querySelector('[role="alert"]')?.textContent)
        .toContain('The selected user cannot be assigned to this task.');
    });

    it('explains that the project must be Active before assigning', async () => {
      await open({ project: { status: ProjectStatus.OnHold } });
      expect(element.querySelector('#task-assignee')).toBeNull();
      expect(element.querySelector('.hint')?.textContent).toContain('The project is On hold. Set it to Active');
    });
  });

  it('deletes after confirmation and returns to the task list', async () => {
    await open();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    button('Delete task').click();
    const del = http.expectOne(TASK);
    expect(del.request.method).toBe('DELETE');
    del.flush(null);

    expect(navigate).toHaveBeenCalledWith(['/projects', 'p1', 'tasks']);
  });
});
