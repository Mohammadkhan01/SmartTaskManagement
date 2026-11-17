import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../api/api.config';
import { AdminUserService } from './admin-user.service';

const API = `${API_BASE_URL}/admin/users`;

describe('AdminUserService', () => {
  let service: AdminUserService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AdminUserService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists users with paging, sort and optional filters', () => {
    service.getUsers({
      pageNumber: 1,
      pageSize: 20,
      search: ' pat ',
      isActive: false,
      role: 'Project Manager',
      sortBy: 'displayName',
      sortDescending: true
    }).subscribe();

    const params = http.expectOne(r => r.url === API).request.params;
    expect(params.get('pageNumber')).toBe('1');
    expect(params.get('pageSize')).toBe('20');
    expect(params.get('sortBy')).toBe('displayName');
    expect(params.get('sortDescending')).toBe('true');
    expect(params.get('search')).toBe('pat');
    expect(params.get('isActive')).toBe('false');
    expect(params.get('role')).toBe('Project Manager');
  });

  it('omits unset filters', () => {
    service.getUsers({ pageNumber: 1, pageSize: 20, sortBy: 'email', sortDescending: false }).subscribe();

    const params = http.expectOne(r => r.url === API).request.params;
    expect(params.has('search')).toBe(false);
    expect(params.has('isActive')).toBe(false);
    expect(params.has('role')).toBe(false);
  });

  it('patches status and puts role for an encoded user id', () => {
    service.updateStatus('u/1', { isActive: false }).subscribe();
    const status = http.expectOne(`${API}/u%2F1/status`);
    expect(status.request.method).toBe('PATCH');
    expect(status.request.body).toEqual({ isActive: false });

    service.updateRole('u1', { role: 'Admin' }).subscribe();
    const role = http.expectOne(`${API}/u1/role`);
    expect(role.request.method).toBe('PUT');
    expect(role.request.body).toEqual({ role: 'Admin' });
  });
});
