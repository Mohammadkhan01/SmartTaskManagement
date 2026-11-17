import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../api/api.config';
import { TaskPriority, TaskStatus } from './task.models';
import { TaskService } from './task.service';

describe('TaskService', () => {
  let service: TaskService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(TaskService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists project tasks with filters, sort and paging params', () => {
    service.getTasks('p1', {
      pageNumber: 3,
      pageSize: 20,
      search: ' bug ',
      status: TaskStatus.InProgress,
      priority: TaskPriority.Critical,
      assignedToUserId: 'u7',
      sortBy: 'dueDate',
      sortDescending: false
    }).subscribe();

    const req = http.expectOne(r => r.url === `${API_BASE_URL}/projects/p1/tasks`);
    const params = req.request.params;
    expect(params.get('pageNumber')).toBe('3');
    expect(params.get('pageSize')).toBe('20');
    expect(params.get('search')).toBe('bug');
    expect(params.get('status')).toBe('2');
    expect(params.get('priority')).toBe('4');
    expect(params.get('assignedToUserId')).toBe('u7');
    expect(params.get('sortBy')).toBe('dueDate');
    expect(params.get('sortDescending')).toBe('false');
    req.flush({ items: [] });
  });

  it('omits optional filters that are not set', () => {
    service.getTasks('p1', { pageNumber: 1, pageSize: 20, search: '', sortBy: 'createdAt', sortDescending: true })
      .subscribe();

    const params = http.expectOne(r => r.url.endsWith('/projects/p1/tasks')).request.params;
    expect(params.keys().sort()).toEqual(['pageNumber', 'pageSize', 'sortBy', 'sortDescending']);
  });

  it('creates tasks under the project and updates them by id', () => {
    const create = {
      title: 'T',
      description: null,
      priority: TaskPriority.High,
      assignedToUserId: null,
      dueDateUtc: null
    };
    service.createTask('p1', create).subscribe();
    const post = http.expectOne(`${API_BASE_URL}/projects/p1/tasks`);
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual(create);

    service.updateTask('t1', { ...create, rowVersion: 'rv' }).subscribe();
    const put = http.expectOne(`${API_BASE_URL}/tasks/t1`);
    expect(put.request.method).toBe('PUT');
    expect(put.request.body.rowVersion).toBe('rv');
  });

  it('gets, assigns, changes status and deletes a task', () => {
    service.getTask('t 1').subscribe();
    expect(http.expectOne(`${API_BASE_URL}/tasks/t%201`).request.method).toBe('GET');

    service.assignTask('t1', { assignedToUserId: 'u2', rowVersion: 'rv' }).subscribe();
    const assign = http.expectOne(`${API_BASE_URL}/tasks/t1/assign`);
    expect(assign.request.method).toBe('PUT');
    expect(assign.request.body).toEqual({ assignedToUserId: 'u2', rowVersion: 'rv' });

    service.updateStatus('t1', { status: TaskStatus.Completed, rowVersion: 'rv' }).subscribe();
    const status = http.expectOne(`${API_BASE_URL}/tasks/t1/status`);
    expect(status.request.method).toBe('PUT');
    expect(status.request.body).toEqual({ status: TaskStatus.Completed, rowVersion: 'rv' });

    service.deleteTask('t1').subscribe();
    expect(http.expectOne(`${API_BASE_URL}/tasks/t1`).request.method).toBe('DELETE');
  });
});
