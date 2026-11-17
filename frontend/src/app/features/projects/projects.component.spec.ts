import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { API_BASE_URL } from '../../core/api/api.config';
import { SessionStore } from '../../core/auth/session-store';
import { Project, ProjectListResponse, ProjectStatus } from '../../core/projects/project.models';
import { ProjectsComponent } from './projects.component';

const API = `${API_BASE_URL}/projects`;

const project = (id: string, overrides: Partial<Project> = {}): Project => ({
  id,
  name: `Project ${id}`,
  description: null,
  status: ProjectStatus.Active,
  createdByUserId: 'u1',
  createdAtUtc: '2026-01-10T09:00:00',
  rowVersion: 'rv',
  ...overrides
});

const page = (items: Project[], overrides: Partial<ProjectListResponse> = {}): ProjectListResponse => ({
  items,
  pageNumber: 1,
  pageSize: 20,
  totalCount: items.length,
  totalPages: 1,
  hasPreviousPage: false,
  hasNextPage: false,
  ...overrides
});

describe('ProjectsComponent', () => {
  let fixture: ComponentFixture<ProjectsComponent>;
  let element: HTMLElement;
  let http: HttpTestingController;

  function create(roles: string[] = ['Project Manager']): void {
    TestBed.inject(SessionStore).setSession('t', { userId: 'u1', displayName: 'Pat', email: 'p@x', roles });
    fixture = TestBed.createComponent(ProjectsComponent);
    element = fixture.nativeElement;
    fixture.detectChanges();
  }

  const expectList = (): TestRequest => http.expectOne(r => r.method === 'GET' && r.url === API);
  const render = () => fixture.detectChanges();
  const button = (label: string) =>
    Array.from(element.querySelectorAll('button')).find(b => b.textContent?.trim() === label)!;

  function select(index: number, value: string): void {
    const el = element.querySelectorAll('select')[index] as HTMLSelectElement;
    el.value = value;
    el.dispatchEvent(new Event('change'));
    render();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ProjectsComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    vi.useRealTimers();
    http.verify();
  });

  it('loads the first page sorted by creation date and shows a loading state', () => {
    create();
    expect(element.querySelector('[role="status"]')?.textContent).toContain('Loading projects');

    const req = expectList();
    expect(req.request.params.get('pageNumber')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('20');
    expect(req.request.params.get('sortBy')).toBe('createdAt');
    expect(req.request.params.get('sortDescending')).toBe('true');
    expect(req.request.params.has('status')).toBe(false);
    req.flush(page([project('a', { description: 'First' }), project('b', { status: ProjectStatus.OnHold })]));
    render();

    const rows = element.querySelectorAll('tbody tr');
    expect(rows).toHaveLength(2);
    expect(rows[0].textContent).toContain('Project a');
    expect(rows[0].textContent).toContain('First');
    expect(rows[0].textContent).toContain('Jan 10, 2026');
    expect(rows[1].textContent).toContain('On hold');
    expect(element.querySelector('.pagination')?.textContent).toContain('2 project(s) · Page 1 of 1');
  });

  it('shows the empty state', () => {
    create();
    expectList().flush(page([]));
    render();
    expect(element.querySelector('.empty h2')?.textContent).toContain('No projects found');
  });

  it('shows an error with retry when loading fails', () => {
    create();
    expectList().flush(null, { status: 500, statusText: 'Error' });
    render();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Could not load projects');

    button('Try again').click();
    expectList().flush(page([project('a')]));
    render();
    expect(element.querySelectorAll('tbody tr')).toHaveLength(1);
  });

  it('debounces search input and requests the first page once', () => {
    create();
    expectList().flush(page([project('a')], { pageNumber: 1, hasNextPage: true, totalPages: 3 }));
    render();

    vi.useFakeTimers();
    const input = element.querySelector<HTMLInputElement>('input[type="search"]')!;
    for (const value of ['a', 'ap', 'apollo']) {
      input.value = value;
      input.dispatchEvent(new Event('input'));
      vi.advanceTimersByTime(100);
    }
    http.expectNone(r => r.url === API);

    vi.advanceTimersByTime(350);
    const req = expectList();
    expect(req.request.params.get('search')).toBe('apollo');
    expect(req.request.params.get('pageNumber')).toBe('1');
    req.flush(page([]));

    // Re-entering the same term after the debounce does not trigger a new request.
    input.value = 'apollo';
    input.dispatchEvent(new Event('input'));
    vi.advanceTimersByTime(400);
    http.expectNone(r => r.url === API);
  });

  it('pages forward and back using pageNumber', () => {
    create();
    expectList().flush(page([project('a')], { pageNumber: 1, totalPages: 2, hasNextPage: true }));
    render();
    expect(button('Previous').disabled).toBe(true);

    button('Next').click();
    const next = expectList();
    expect(next.request.params.get('pageNumber')).toBe('2');
    next.flush(page([project('b')], { pageNumber: 2, totalPages: 2, hasPreviousPage: true }));
    render();
    expect(button('Next').disabled).toBe(true);

    button('Previous').click();
    expect(expectList().request.params.get('pageNumber')).toBe('1');
  });

  it('filters by status and sorts, resetting to page 1', () => {
    create();
    expectList().flush(page([project('a')], { pageNumber: 1, hasNextPage: true }));
    render();
    button('Next').click();
    expectList().flush(page([project('b')], { pageNumber: 2 }));
    render();

    select(0, '2');
    const filtered = expectList();
    expect(filtered.request.params.get('status')).toBe('2');
    expect(filtered.request.params.get('pageNumber')).toBe('1');
    filtered.flush(page([]));

    select(1, 'name');
    const sorted = expectList();
    expect(sorted.request.params.get('sortBy')).toBe('name');
    expect(sorted.request.params.get('status')).toBe('2');
    sorted.flush(page([]));
  });

  it('opens project details', () => {
    create();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    expectList().flush(page([project('p9')]));
    render();

    button('View project').click();
    expect(navigate).toHaveBeenCalledWith(['/projects', 'p9']);
  });

  describe('create project', () => {
    it('is hidden for team members', () => {
      create(['Team Member']);
      expectList().flush(page([]));
      render();
      expect(button('Create project')).toBeUndefined();
    });

    it('validates the name and posts a trimmed payload, then reloads', () => {
      create(['Admin']);
      expectList().flush(page([]));
      render();

      button('Create project').click();
      render();
      const submit = element.querySelector<HTMLButtonElement>('form button[type="submit"]')!;
      expect(submit.disabled).toBe(true);

      const name = element.querySelector<HTMLInputElement>('#project-name')!;
      name.value = '  Apollo  ';
      name.dispatchEvent(new Event('input'));
      const description = element.querySelector<HTMLTextAreaElement>('#project-description')!;
      description.value = '   ';
      description.dispatchEvent(new Event('input'));
      render();
      expect(submit.disabled).toBe(false);

      element.querySelector('form')!.dispatchEvent(new Event('submit'));
      const post = http.expectOne(r => r.method === 'POST' && r.url === API);
      expect(post.request.body).toEqual({ name: 'Apollo', description: null });
      post.flush(project('new'));

      expectList().flush(page([project('new')]));
      render();
      expect(element.querySelector('#project-name')).toBeNull();
    });

    it('shows a permission error when creation is forbidden', () => {
      create(['Project Manager']);
      expectList().flush(page([]));
      render();
      button('Create project').click();
      render();

      fixture.componentInstance.createForm.setValue({ name: 'X', description: '' });
      element.querySelector('form')!.dispatchEvent(new Event('submit'));
      http.expectOne(r => r.method === 'POST').flush(null, { status: 403, statusText: 'Forbidden' });
      render();

      expect(element.querySelector('.create-panel [role="alert"]')?.textContent)
        .toContain('You do not have permission');
    });
  });
});
