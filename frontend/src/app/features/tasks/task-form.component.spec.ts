import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AI_DESCRIPTION_MAX_LENGTH } from '../../core/ai/ai.service';
import { API_BASE_URL } from '../../core/api/api.config';
import { Project, ProjectMemberRole, ProjectStatus } from '../../core/projects/project.models';
import { Task, TaskPriority, TaskStatus } from '../../core/tasks/task.models';
import { TaskFormComponent } from './task-form.component';

const AI = `${API_BASE_URL}/ai/enhance-task-description`;
const members = [{ userId: 'u2', displayName: 'Sam', email: 'sam@x', projectRole: ProjectMemberRole.Member }];
const activeProject: Project = {
  id: 'p1',
  name: 'Project One',
  description: null,
  status: ProjectStatus.Active,
  createdByUserId: 'u1',
  createdAtUtc: '2026-01-01T00:00:00Z',
  rowVersion: 'rv-p1'
};

const existing: Task = {
  id: 't1',
  projectId: 'p1',
  title: 'Fix login',
  description: 'login broke',
  status: TaskStatus.InProgress,
  priority: TaskPriority.High,
  assignedToUserId: 'u2',
  createdByUserId: 'u1',
  dueDateUtc: '2026-05-06T12:00:00',
  createdAtUtc: '2026-01-01T00:00:00',
  updatedAtUtc: null,
  rowVersion: 'rv-1'
};

describe('TaskFormComponent', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;
  let element: HTMLElement;
  let component: TaskFormComponent;
  let navigate: ReturnType<typeof vi.spyOn>;

  const render = () => harness.detectChanges();
  const button = (label: string) =>
    Array.from(element.querySelectorAll('button')).find(b => b.textContent?.trim() === label)!;
  const enhanceButton = () => element.querySelector<HTMLButtonElement>('.ai-button')!;
  const submit = () => element.querySelector<HTMLButtonElement>('button[type="submit"]')!;

  function type(selector: string, value: string): void {
    const el = element.querySelector<HTMLInputElement | HTMLTextAreaElement>(selector)!;
    el.value = value;
    el.dispatchEvent(new Event('input'));
    el.dispatchEvent(new Event('blur'));
    render();
  }

  function flushProjectAndMembers(
    membersOk = true,
    project: Project = activeProject
  ): void {
    http.expectOne(`${API_BASE_URL}/projects/p1`).flush(project);
    const membersReq = http.expectOne(`${API_BASE_URL}/projects/p1/members`);
    if (membersOk) membersReq.flush(members);
    else membersReq.flush(null, { status: 403, statusText: 'Forbidden' });
  }

  async function openCreate(
    membersOk = true,
    project: Project = activeProject
  ): Promise<void> {
    harness = await RouterTestingHarness.create();
    component = await harness.navigateByUrl('/projects/p1/tasks/new', TaskFormComponent);
    element = harness.routeNativeElement!;
    flushProjectAndMembers(membersOk, project);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    render();
  }

  async function openEdit(): Promise<void> {
    harness = await RouterTestingHarness.create();
    component = await harness.navigateByUrl('/tasks/t1/edit', TaskFormComponent);
    element = harness.routeNativeElement!;
    http.expectOne(`${API_BASE_URL}/tasks/t1`).flush(existing);
    flushProjectAndMembers();
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    render();
  }

  async function requestSuggestion(suggestion: string): Promise<void> {
    type('#task-title', 'Fix login');
    type('#task-description', '  login broke  ');
    enhanceButton().click();
    render();
    const req = http.expectOne(AI);
    expect(req.request.body).toEqual({ title: 'Fix login', description: 'login broke' });
    req.flush({ enhancedDescription: suggestion });
    render();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'projects/:id/tasks/new', component: TaskFormComponent },
          { path: 'tasks/:taskId/edit', component: TaskFormComponent }
        ]),
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

  describe('validation', () => {
    it('requires a title', async () => {
      await openCreate();
      expect(element.querySelector('h1')?.textContent).toContain('Create a task');
      expect(submit().disabled).toBe(true);

      type('#task-title', '');
      expect(element.textContent).toContain('Enter a title (maximum 200 characters).');

      type('#task-title', 'Ship it');
      expect(submit().disabled).toBe(false);
    });

    it('enforces title and description maximum lengths', async () => {
      await openCreate();
      component.form.controls.title.setValue('x'.repeat(201));
      component.form.controls.description.setValue('y'.repeat(4001));
      component.form.controls.title.markAsTouched();
      render();

      expect(component.form.controls.title.hasError('maxlength')).toBe(true);
      expect(component.form.controls.description.hasError('maxlength')).toBe(true);
      expect(element.textContent).toContain('The description cannot exceed 4000 characters.');
      expect(submit().disabled).toBe(true);

      component.form.controls.title.setValue('x'.repeat(200));
      component.form.controls.description.setValue('y'.repeat(4000));
      render();
      expect(submit().disabled).toBe(false);
      expect(element.textContent).toContain('4000 / 4000 characters');
    });

    it('rejects a whitespace-only title on save', async () => {
      await openCreate();
      type('#task-title', '   ');
      component.save();
      render();

      expect(component.form.controls.title.hasError('required')).toBe(true);
      http.expectNone(r => r.method === 'POST');
    });
  });

  describe('create', () => {
    it('posts a trimmed payload with UTC-noon due date and assignee, then opens the task', async () => {
      await openCreate();
      type('#task-title', '  Write tests  ');
      type('#task-description', '  Cover the guards.  ');
      const priority = element.querySelector<HTMLSelectElement>('#task-priority')!;
      priority.selectedIndex = Array.from(priority.options).findIndex(o => o.textContent?.trim() === 'Critical');
      priority.dispatchEvent(new Event('change'));
      type('#task-due', '2026-07-01');
      const assignee = element.querySelector<HTMLSelectElement>('#task-assignee')!;
      assignee.value = 'u2';
      assignee.dispatchEvent(new Event('change'));
      render();

      element.querySelector('form')!.dispatchEvent(new Event('submit'));
      render();
      expect(submit().textContent).toContain('Saving');

      const post = http.expectOne(`${API_BASE_URL}/projects/p1/tasks`);
      expect(post.request.method).toBe('POST');
      expect(post.request.body).toEqual({
        title: 'Write tests',
        description: 'Cover the guards.',
        priority: TaskPriority.Critical,
        dueDateUtc: '2026-07-01T12:00:00Z',
        assignedToUserId: 'u2'
      });
      post.flush({ ...existing, id: 'new-task' });

      expect(navigate).toHaveBeenCalledWith(['/tasks', 'new-task']);
    });

    it('sends nulls for empty optional fields and hides the assignee when members are not visible', async () => {
      await openCreate(false);
      expect(element.querySelector('#task-assignee')).toBeNull();
      expect(element.textContent).toContain('Project members are only visible to project managers.');

      type('#task-title', 'Minimal');
      element.querySelector('form')!.dispatchEvent(new Event('submit'));

      expect(http.expectOne(`${API_BASE_URL}/projects/p1/tasks`).request.body).toEqual({
        title: 'Minimal',
        description: null,
        priority: TaskPriority.Medium,
        dueDateUtc: null,
        assignedToUserId: null
      });
    });

    it('forces unassigned create payload when the project is not Active', async () => {
      await openCreate(true, { ...activeProject, status: ProjectStatus.OnHold });
      expect(element.textContent).toContain('This project is not Active');
      expect(component.form.controls.assignedToUserId.disabled).toBe(true);

      type('#task-title', 'No assignee yet');
      element.querySelector('form')!.dispatchEvent(new Event('submit'));

      expect(http.expectOne(`${API_BASE_URL}/projects/p1/tasks`).request.body).toEqual({
        title: 'No assignee yet',
        description: null,
        priority: TaskPriority.Medium,
        dueDateUtc: null,
        assignedToUserId: null
      });
    });

    it('shows the API validation message on 400', async () => {
      await openCreate();
      type('#task-title', 'Bad');
      element.querySelector('form')!.dispatchEvent(new Event('submit'));
      http.expectOne(`${API_BASE_URL}/projects/p1/tasks`).flush(
        { errors: { DueDateUtc: ['Due date cannot be in the past.'] } },
        { status: 400, statusText: 'Bad Request' }
      );
      render();

      expect(element.querySelector('form [role="alert"]')?.textContent).toContain('Due date cannot be in the past.');
      expect(navigate).not.toHaveBeenCalled();
      expect(submit().disabled).toBe(false);
    });
  });

  describe('edit', () => {
    it('loads the task into the form', async () => {
      await openEdit();
      expect(element.querySelector('h1')?.textContent).toContain('Edit task');
      expect(component.form.getRawValue()).toEqual({
        title: 'Fix login',
        description: 'login broke',
        priority: TaskPriority.High,
        dueDate: '2026-05-06',
        assignedToUserId: 'u2'
      });
      expect(submit().textContent).toContain('Save changes');
    });

    it('puts the updated payload with the row version', async () => {
      await openEdit();
      type('#task-title', 'Fix login page');

      element.querySelector('form')!.dispatchEvent(new Event('submit'));
      const put = http.expectOne(`${API_BASE_URL}/tasks/t1`);
      expect(put.request.method).toBe('PUT');
      expect(put.request.body).toEqual({
        title: 'Fix login page',
        description: 'login broke',
        priority: TaskPriority.High,
        dueDateUtc: '2026-05-06T12:00:00Z',
        assignedToUserId: 'u2',
        rowVersion: 'rv-1'
      });
      put.flush(existing);
      expect(navigate).toHaveBeenCalledWith(['/tasks', 't1']);
    });

    it('shows the concurrency message on 409', async () => {
      await openEdit();
      element.querySelector('form')!.dispatchEvent(new Event('submit'));
      http.expectOne(`${API_BASE_URL}/tasks/t1`).flush(null, { status: 409, statusText: 'Conflict' });
      render();

      expect(element.querySelector('form [role="alert"]')?.textContent)
        .toContain('This task was changed by someone else.');
    });

    it('shows a load error when the task is missing', async () => {
      harness = await RouterTestingHarness.create();
      await harness.navigateByUrl('/tasks/t1/edit', TaskFormComponent);
      element = harness.routeNativeElement!;
      http.expectOne(`${API_BASE_URL}/tasks/t1`).flush(null, { status: 404, statusText: 'Not Found' });
      render();

      expect(element.querySelector('[role="alert"]')?.textContent)
        .toContain('Task not found or you do not have access to it.');
      expect(element.querySelector('form')).toBeNull();
    });
  });

  describe('Enhance with AI', () => {
    it('is disabled until there is a description', async () => {
      await openCreate();
      expect(enhanceButton().disabled).toBe(true);
      type('#task-description', '   ');
      expect(enhanceButton().disabled).toBe(true);
      type('#task-description', 'do it');
      expect(enhanceButton().disabled).toBe(false);
    });

    it('enables enhancement up to the 4000-character AI limit and disables it beyond', async () => {
      await openCreate();
      component.form.controls.description.setValue('a'.repeat(AI_DESCRIPTION_MAX_LENGTH));
      render();
      expect(enhanceButton().disabled).toBe(false);

      component.form.controls.description.setValue('a'.repeat(AI_DESCRIPTION_MAX_LENGTH + 1));
      render();
      expect(enhanceButton().disabled).toBe(true);
    });

    it('shows a busy state while the suggestion is requested', async () => {
      await openCreate();
      type('#task-description', 'do it');
      enhanceButton().click();
      render();

      expect(enhanceButton().disabled).toBe(true);
      expect(enhanceButton().textContent).toContain('Enhancing');
      expect(enhanceButton().getAttribute('aria-busy')).toBe('true');
      http.expectOne(AI).flush({ enhancedDescription: 'Do it well.' });
    });

    it('replaces the description only when the suggestion is accepted', async () => {
      await openCreate();
      await requestSuggestion('Investigate and fix the login failure.');

      const panel = element.querySelector('.suggestion')!;
      expect(panel.textContent).toContain('Investigate and fix the login failure.');
      expect(component.form.controls.description.value).toBe('  login broke  ');

      button('Use suggestion').click();
      render();

      expect(component.form.controls.description.value).toBe('Investigate and fix the login failure.');
      expect(component.form.controls.description.dirty).toBe(true);
      expect(element.querySelector('.suggestion')).toBeNull();
      http.expectNone(r => r.method === 'POST' || r.method === 'PUT');
    });

    it('keeps the original description when the suggestion is discarded', async () => {
      await openCreate();
      await requestSuggestion('Something else entirely.');

      button('Discard').click();
      render();

      expect(component.form.controls.description.value).toBe('  login broke  ');
      expect(element.querySelector('.suggestion')).toBeNull();
    });

    it('disables "Use suggestion" when the suggestion exceeds 4000 characters', async () => {
      await openCreate();
      await requestSuggestion('z'.repeat(4001));

      expect(button('Use suggestion').disabled).toBe(true);
      expect(element.querySelector('.suggestion .error')?.textContent)
        .toContain('This suggestion is 4001 characters, which exceeds the 4000-character');

      component.acceptSuggestion();
      expect(component.form.controls.description.value).toBe('  login broke  ');

      button('Discard').click();
      render();
      expect(element.querySelector('.suggestion')).toBeNull();
    });

    it('allows a suggestion of exactly 4000 characters', async () => {
      await openCreate();
      await requestSuggestion('z'.repeat(4000));
      expect(button('Use suggestion').disabled).toBe(false);
    });

    it('reports an empty suggestion without changing the description', async () => {
      await openCreate();
      await requestSuggestion('   ');

      expect(element.querySelector('.suggestion')).toBeNull();
      expect(element.querySelector('[role="alert"]')?.textContent).toContain('returned an empty suggestion');
      expect(component.form.controls.description.value).toBe('  login broke  ');
    });

    it.each([
      [429, 'Too many AI requests. Wait a moment and try again.'],
      [503, 'The AI service is unavailable or timed out.'],
      [0, 'Cannot reach the server for AI enhancement.'],
      [500, 'AI enhancement failed. Your description was not changed.']
    ])('shows a friendly message when the AI call fails with %s', async (status, message) => {
      await openCreate();
      type('#task-description', 'do it');
      enhanceButton().click();
      if (status === 0) {
        http.expectOne(AI).error(new ProgressEvent('error'), { status: 0 });
      } else {
        http.expectOne(AI).flush(null, { status, statusText: 'Error' });
      }
      render();

      expect(element.querySelector('[role="alert"]')?.textContent).toContain(message);
      expect(component.form.controls.description.value).toBe('do it');
      expect(enhanceButton().disabled).toBe(false);
    });
  });
});
