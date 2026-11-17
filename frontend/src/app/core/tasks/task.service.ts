import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../api/api.config';
import {
  AssignTaskRequest,
  CreateTaskRequest,
  Task,
  TaskListQuery,
  TaskListResponse,
  UpdateTaskRequest,
  UpdateTaskStatusRequest
} from './task.models';

const PROJECTS_API = `${API_BASE_URL}/projects`;
const TASKS_API = `${API_BASE_URL}/tasks`;

@Injectable({ providedIn: 'root' })
export class TaskService {
  private readonly http = inject(HttpClient);

  getTasks(projectId: string, query: TaskListQuery): Observable<TaskListResponse> {
    let params = new HttpParams()
      .set('pageNumber', query.pageNumber)
      .set('pageSize', query.pageSize)
      .set('sortBy', query.sortBy)
      .set('sortDescending', query.sortDescending);

    if (query.search?.trim()) {
      params = params.set('search', query.search.trim());
    }

    if (query.status !== undefined) {
      params = params.set('status', query.status);
    }

    if (query.priority !== undefined) {
      params = params.set('priority', query.priority);
    }

    if (query.assignedToUserId) {
      params = params.set('assignedToUserId', query.assignedToUserId);
    }

    return this.http.get<TaskListResponse>(
      `${PROJECTS_API}/${encodeURIComponent(projectId)}/tasks`,
      { params }
    );
  }

  getTask(taskId: string): Observable<Task> {
    return this.http.get<Task>(`${TASKS_API}/${encodeURIComponent(taskId)}`);
  }

  createTask(projectId: string, request: CreateTaskRequest): Observable<Task> {
    return this.http.post<Task>(
      `${PROJECTS_API}/${encodeURIComponent(projectId)}/tasks`,
      request
    );
  }

  updateTask(taskId: string, request: UpdateTaskRequest): Observable<Task> {
    return this.http.put<Task>(
      `${TASKS_API}/${encodeURIComponent(taskId)}`,
      request
    );
  }

  assignTask(taskId: string, request: AssignTaskRequest): Observable<Task> {
    return this.http.put<Task>(
      `${TASKS_API}/${encodeURIComponent(taskId)}/assign`,
      request
    );
  }

  updateStatus(taskId: string, request: UpdateTaskStatusRequest): Observable<Task> {
    return this.http.put<Task>(
      `${TASKS_API}/${encodeURIComponent(taskId)}/status`,
      request
    );
  }

  deleteTask(taskId: string): Observable<void> {
    return this.http.delete<void>(`${TASKS_API}/${encodeURIComponent(taskId)}`);
  }
}
