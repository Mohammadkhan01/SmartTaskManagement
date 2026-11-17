import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../api/api.config';
import {
  AddProjectMemberRequest,
  CreateProjectRequest,
  Project,
  ProjectListQuery,
  ProjectListResponse,
  ProjectMember,
  UpdateProjectRequest
} from './project.models';

const API = `${API_BASE_URL}/projects`;

@Injectable({ providedIn: 'root' })
export class ProjectService {
  private readonly http = inject(HttpClient);

  getProjects(query: ProjectListQuery): Observable<ProjectListResponse> {
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

    return this.http.get<ProjectListResponse>(API, { params });
  }

  getProject(projectId: string): Observable<Project> {
    return this.http.get<Project>(`${API}/${encodeURIComponent(projectId)}`);
  }

  createProject(request: CreateProjectRequest): Observable<Project> {
    return this.http.post<Project>(API, request);
  }

  updateProject(
    projectId: string,
    request: UpdateProjectRequest
  ): Observable<Project> {
    return this.http.put<Project>(
      `${API}/${encodeURIComponent(projectId)}`,
      request
    );
  }

  deleteProject(projectId: string): Observable<void> {
    return this.http.delete<void>(
      `${API}/${encodeURIComponent(projectId)}`
    );
  }

  getMembers(projectId: string): Observable<ProjectMember[]> {
    return this.http.get<ProjectMember[]>(
      `${API}/${encodeURIComponent(projectId)}/members`
    );
  }

  addMember(
    projectId: string,
    request: AddProjectMemberRequest
  ): Observable<ProjectMember> {
    return this.http.post<ProjectMember>(
      `${API}/${encodeURIComponent(projectId)}/members`,
      request
    );
  }

  removeMember(projectId: string, userId: string): Observable<void> {
    return this.http.delete<void>(
      `${API}/${encodeURIComponent(projectId)}/members/${encodeURIComponent(userId)}`
    );
  }
}
