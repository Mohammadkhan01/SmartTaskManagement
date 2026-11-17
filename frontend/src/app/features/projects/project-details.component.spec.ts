import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { API_BASE_URL } from '../../core/api/api.config';
import { Project, ProjectMemberRole, ProjectStatus } from '../../core/projects/project.models';
import { ProjectDetailsComponent } from './project-details.component';

const API = `${API_BASE_URL}/projects`;

const project: Project = {
  id: 'p1',
  name: 'Apollo',
  description: 'Moon shot',
  status: ProjectStatus.Planning,
  createdByUserId: 'u1',
  createdAtUtc: '2026-01-10T09:00:00',
  rowVersion: 'rv-1'
};

const members = [
  { userId: 'u1', displayName: 'Pat', email: 'pat@x', projectRole: ProjectMemberRole.Manager },
  { userId: 'u2', displayName: 'Sam', email: 'sam@x', projectRole: ProjectMemberRole.Member }
];

describe('ProjectDetailsComponent', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;
  let element: HTMLElement;

  async function open(): Promise<void> {
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/projects/p1', ProjectDetailsComponent);
    element = harness.routeNativeElement!;
  }

  async function openLoaded(memberResponse: 'ok' | 403 = 'ok'): Promise<void> {
    await open();
    http.expectOne(`${API}/p1`).flush(project);
    const membersReq = http.expectOne(`${API}/p1/members`);
    if (memberResponse === 'ok') {
      membersReq.flush(members);
    } else {
      membersReq.flush(null, { status: 403, statusText: 'Forbidden' });
    }
    harness.detectChanges();
  }

  const button = (label: string) =>
    Array.from(element.querySelectorAll('button')).find(b => b.textContent?.trim() === label)!;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'projects/:id', component: ProjectDetailsComponent }]),
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

  it('shows a loading state, then the project and its members', async () => {
    await open();
    expect(element.querySelector('[role="status"]')?.textContent).toContain('Loading project');

    http.expectOne(`${API}/p1`).flush(project);
    http.expectOne(`${API}/p1/members`).flush(members);
    harness.detectChanges();

    expect(element.querySelector('h1')?.textContent).toContain('Apollo');
    expect(element.querySelector('.status')?.textContent).toContain('Planning');
    expect(element.querySelector('.description')?.textContent).toContain('Moon shot');
    expect(element.querySelector('.hint')?.textContent).toContain('Set it to Active to assign tasks');
    expect(element.querySelectorAll('.member')).toHaveLength(2);
    expect(element.querySelector('a[href="/projects/p1/tasks"]')).not.toBeNull();
  });

  it('shows a not-found message for 404', async () => {
    await open();
    http.expectOne(`${API}/p1`).flush(null, { status: 404, statusText: 'Not Found' });
    harness.detectChanges();

    expect(element.querySelector('[role="alert"]')?.textContent)
      .toContain('Project not found or you do not have access to it.');
  });

  it('explains when the member list is not permitted', async () => {
    await openLoaded(403);
    expect(element.textContent).toContain("You do not have permission to manage this project's members.");
  });

  it('updates the project status via the status selector and sends the row version', async () => {
    await openLoaded();
    button('Edit project').click();
    harness.detectChanges();

    const status = element.querySelector<HTMLSelectElement>('#status')!;
    const activeIndex = Array.from(status.options).findIndex(o => o.textContent?.trim() === 'Active');
    status.selectedIndex = activeIndex;
    status.dispatchEvent(new Event('change'));
    harness.detectChanges();

    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    const put = http.expectOne(`${API}/p1`);
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({
      name: 'Apollo',
      description: 'Moon shot',
      status: ProjectStatus.Active,
      rowVersion: 'rv-1'
    });
    put.flush({ ...project, status: ProjectStatus.Active, rowVersion: 'rv-2' });
    harness.detectChanges();

    expect(element.querySelector('#status')).toBeNull();
    expect(element.querySelector('.status')?.textContent).toContain('Active');
    expect(element.querySelector('.hint')).toBeNull();
  });

  it('shows the concurrency message when the save returns 409', async () => {
    await openLoaded();
    button('Edit project').click();
    harness.detectChanges();

    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    http.expectOne(`${API}/p1`).flush(null, { status: 409, statusText: 'Conflict' });
    harness.detectChanges();

    expect(element.querySelector('form [role="alert"]')?.textContent)
      .toContain('This project was changed by someone else. Reload the latest version before editing again.');
    expect(button('Save changes').disabled).toBe(false);
  });

  it('requires a name before saving', async () => {
    await openLoaded();
    button('Edit project').click();
    harness.detectChanges();

    const name = element.querySelector<HTMLInputElement>('#name')!;
    name.value = '';
    name.dispatchEvent(new Event('input'));
    name.dispatchEvent(new Event('blur'));
    harness.detectChanges();

    expect(button('Save changes').disabled).toBe(true);
    expect(element.textContent).toContain('A project name is required');
  });

  it('deletes after confirmation and returns to the list', async () => {
    await openLoaded();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const confirm = vi.spyOn(window, 'confirm').mockReturnValueOnce(false).mockReturnValueOnce(true);

    button('Delete project').click();
    http.expectNone(`${API}/p1`);

    button('Delete project').click();
    expect(confirm).toHaveBeenCalledTimes(2);
    const del = http.expectOne(`${API}/p1`);
    expect(del.request.method).toBe('DELETE');
    del.flush(null);

    expect(navigate).toHaveBeenCalledWith(['/projects']);
  });

  it('adds and removes members', async () => {
    await openLoaded();
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    const input = element.querySelector<HTMLInputElement>('#memberUserId')!;
    input.value = ' u3 ';
    input.dispatchEvent(new Event('input'));
    harness.detectChanges();
    button('Add member').click();
    const add = http.expectOne(r => r.method === 'POST' && r.url === `${API}/p1/members`);
    expect(add.request.body).toEqual({ userId: 'u3' });
    add.flush({ userId: 'u3', displayName: 'Lee', email: 'lee@x', projectRole: ProjectMemberRole.Member });
    harness.detectChanges();
    expect(element.querySelectorAll('.member')).toHaveLength(3);

    element.querySelectorAll<HTMLButtonElement>('.member button')[1].click();
    http.expectOne(r => r.method === 'DELETE' && r.url === `${API}/p1/members/u2`).flush(null);
    harness.detectChanges();
    expect(element.textContent).not.toContain('Sam');
  });
});
