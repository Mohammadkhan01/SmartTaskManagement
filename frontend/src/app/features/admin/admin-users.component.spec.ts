import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AdminUser, AdminUserListResponse } from '../../core/admin/admin-user.models';
import { API_BASE_URL } from '../../core/api/api.config';
import { SessionStore } from '../../core/auth/session-store';
import { AdminUsersComponent } from './admin-users.component';

const API = `${API_BASE_URL}/admin/users`;

const users: AdminUser[] = [
  { userId: 'me', displayName: 'Admin Pat', email: 'pat@x', isActive: true, roles: ['Admin'] },
  { userId: 'u2', displayName: 'Sam', email: 'sam@x', isActive: true, roles: ['Team Member'] },
  { userId: 'u3', displayName: '', email: 'lee@x', isActive: false, roles: [] }
];

const page = (items: AdminUser[], overrides: Partial<AdminUserListResponse> = {}): AdminUserListResponse => ({
  items,
  pageNumber: 1,
  pageSize: 20,
  totalCount: items.length,
  totalPages: 1,
  hasPreviousPage: false,
  hasNextPage: false,
  ...overrides
});

describe('AdminUsersComponent', () => {
  let fixture: ComponentFixture<AdminUsersComponent>;
  let element: HTMLElement;
  let http: HttpTestingController;

  const render = () => fixture.detectChanges();
  const expectList = (): TestRequest => http.expectOne(r => r.method === 'GET' && r.url === API);
  const rows = () => Array.from(element.querySelectorAll<HTMLTableRowElement>('tbody tr'));
  const rowFor = (email: string) => rows().find(row => row.textContent?.includes(email))!;

  function filter(label: string, value: string): void {
    const select = Array.from(element.querySelectorAll('.filters label'))
      .find(l => l.querySelector('span')?.textContent === label)!
      .querySelector('select')!;
    select.value = value;
    select.dispatchEvent(new Event('change'));
    render();
  }

  function create(firstPage = page(users)): void {
    fixture = TestBed.createComponent(AdminUsersComponent);
    element = fixture.nativeElement;
    render();
    expectList().flush(firstPage);
    render();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [AdminUsersComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    http = TestBed.inject(HttpTestingController);
    TestBed.inject(SessionStore).setSession('t', {
      userId: 'me', displayName: 'Admin Pat', email: 'pat@x', roles: ['Admin']
    });
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    http.verify();
  });

  it('loads users sorted by email and renders status and roles', () => {
    fixture = TestBed.createComponent(AdminUsersComponent);
    element = fixture.nativeElement;
    render();
    expect(element.querySelector('[role="status"]')?.textContent).toContain('Loading users');

    const req = expectList();
    expect(req.request.params.get('sortBy')).toBe('email');
    expect(req.request.params.get('sortDescending')).toBe('false');
    expect(req.request.params.get('pageNumber')).toBe('1');
    req.flush(page(users));
    render();

    expect(rows()).toHaveLength(3);
    expect(rowFor('pat@x').textContent).toContain('Admin Pat (you)');
    expect(rowFor('lee@x').textContent).toContain('(no name)');
    expect(rowFor('lee@x').querySelector('.badge')?.textContent).toContain('Inactive');
    expect(rowFor('sam@x').querySelector<HTMLSelectElement>('select')!.value).toBe('Team Member');
  });

  it('prevents administrators from deactivating themselves', () => {
    create();
    const self = rowFor('pat@x').querySelector<HTMLButtonElement>('td:last-child button')!;
    expect(self.disabled).toBe(true);
    expect(self.title).toContain('You cannot deactivate your own account.');
  });

  it('sends role, status and sort filters', () => {
    create(page(users, { hasNextPage: true, totalPages: 2 }));
    const next = Array.from(element.querySelectorAll('button')).find(b => b.textContent?.trim() === 'Next')!;
    next.click();
    expect(expectList().request.params.get('pageNumber')).toBe('2');

    filter('Role', 'Project Manager');
    let req = expectList();
    expect(req.request.params.get('role')).toBe('Project Manager');
    expect(req.request.params.get('pageNumber')).toBe('1');
    req.flush(page([]));

    filter('Account status', 'false');
    req = expectList();
    expect(req.request.params.get('isActive')).toBe('false');
    req.flush(page([]));
    render();
    expect(element.querySelector('.empty h2')?.textContent).toContain('No users found');

    filter('Sort by', 'displayName');
    expect(expectList().request.params.get('sortBy')).toBe('displayName');
  });

  it('debounces the search box', () => {
    create();
    vi.useFakeTimers();
    const input = element.querySelector<HTMLInputElement>('input[type="search"]')!;
    input.value = 'sam';
    input.dispatchEvent(new Event('input'));
    vi.advanceTimersByTime(300);
    http.expectNone(r => r.url === API);

    vi.advanceTimersByTime(50);
    expect(expectList().request.params.get('search')).toBe('sam');
  });

  it('deactivates a user after confirmation', () => {
    create();
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
    rowFor('sam@x').querySelector<HTMLButtonElement>('td:last-child button')!.click();
    render();

    expect(confirm).toHaveBeenCalledWith('Deactivate the account for sam@x?');
    expect(rowFor('sam@x').querySelector('td:last-child button')?.textContent).toContain('Saving');
    const patch = http.expectOne(`${API}/u2/status`);
    expect(patch.request.method).toBe('PATCH');
    expect(patch.request.body).toEqual({ isActive: false });
    patch.flush({ ...users[1], isActive: false });
    render();

    expect(rowFor('sam@x').querySelector('.badge')?.textContent).toContain('Inactive');
    expect(element.querySelector('.success')?.textContent).toContain('sam@x is now inactive.');
  });

  it('does nothing when the status change is not confirmed', () => {
    create();
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    rowFor('lee@x').querySelector<HTMLButtonElement>('td:last-child button')!.click();
    http.expectNone(`${API}/u3/status`);
  });

  it('changes a role after confirmation', () => {
    create();
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    const select = rowFor('sam@x').querySelector<HTMLSelectElement>('select')!;
    select.value = 'Project Manager';
    select.dispatchEvent(new Event('change'));

    const put = http.expectOne(`${API}/u2/role`);
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ role: 'Project Manager' });
    put.flush({ ...users[1], roles: ['Project Manager'] });
    render();

    expect(element.querySelector('.success')?.textContent).toContain('sam@x is now Project Manager.');
  });

  it('reverts the role select when the change is cancelled', () => {
    create();
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    const select = rowFor('sam@x').querySelector<HTMLSelectElement>('select')!;
    select.value = 'Admin';
    select.dispatchEvent(new Event('change'));

    expect(select.value).toBe('Team Member');
    http.expectNone(`${API}/u2/role`);
  });

  it('shows the API detail and reverts the select when the change conflicts', () => {
    create();
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    const select = rowFor('pat@x').querySelector<HTMLSelectElement>('select')!;
    select.value = 'Team Member';
    select.dispatchEvent(new Event('change'));

    http.expectOne(`${API}/me/role`).flush(
      { detail: 'Cannot remove the last active administrator.' },
      { status: 409, statusText: 'Conflict' }
    );
    render();

    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Cannot remove the last active administrator.');
    expect(select.value).toBe('Admin');
    expect(select.disabled).toBe(false);
  });

  it('shows a permission error when the list is forbidden', () => {
    fixture = TestBed.createComponent(AdminUsersComponent);
    element = fixture.nativeElement;
    render();
    expectList().flush(null, { status: 403, statusText: 'Forbidden' });
    render();

    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Only administrators can manage users.');
  });
});
