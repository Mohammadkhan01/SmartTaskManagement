import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../api/api.config';
import { ProjectStatus } from './project.models';
import { ProjectService } from './project.service';

const API = `${API_BASE_URL}/projects`;

describe('ProjectService', () => {
  let service: ProjectService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(ProjectService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends paging and sort params, trimming the search and including status', () => {
    service.getProjects({
      pageNumber: 2,
      pageSize: 20,
      search: '  apollo  ',
      status: ProjectStatus.Active,
      sortBy: 'name',
      sortDescending: false
    }).subscribe();

    const req = http.expectOne(r => r.url === API);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('pageNumber')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('20');
    expect(req.request.params.get('sortBy')).toBe('name');
    expect(req.request.params.get('sortDescending')).toBe('false');
    expect(req.request.params.get('search')).toBe('apollo');
    expect(req.request.params.get('status')).toBe('2');
    req.flush({ items: [] });
  });

  it('omits blank search and missing status', () => {
    service.getProjects({ pageNumber: 1, pageSize: 20, search: '   ', sortBy: 'createdAt', sortDescending: true })
      .subscribe();

    const req = http.expectOne(r => r.url === API);
    expect(req.request.params.has('search')).toBe(false);
    expect(req.request.params.has('status')).toBe(false);
    req.flush({ items: [] });
  });

  it('gets, creates, updates and deletes projects with encoded ids', () => {
    service.getProject('a/b').subscribe();
    http.expectOne(`${API}/a%2Fb`).flush({});

    service.createProject({ name: 'New', description: null }).subscribe();
    const create = http.expectOne(API);
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({ name: 'New', description: null });
    create.flush({});

    const update = { name: 'N', description: null, status: ProjectStatus.Completed, rowVersion: 'rv' };
    service.updateProject('p1', update).subscribe();
    const put = http.expectOne(`${API}/p1`);
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual(update);
    put.flush({});

    service.deleteProject('p1').subscribe();
    expect(http.expectOne(`${API}/p1`).request.method).toBe('DELETE');
  });

  it('manages project members', () => {
    service.getMembers('p1').subscribe();
    expect(http.expectOne(`${API}/p1/members`).request.method).toBe('GET');

    service.addMember('p1', { userId: 'u2' }).subscribe();
    const add = http.expectOne(`${API}/p1/members`);
    expect(add.request.method).toBe('POST');
    expect(add.request.body).toEqual({ userId: 'u2' });

    service.removeMember('p1', 'u2').subscribe();
    expect(http.expectOne(`${API}/p1/members/u2`).request.method).toBe('DELETE');
  });
});
