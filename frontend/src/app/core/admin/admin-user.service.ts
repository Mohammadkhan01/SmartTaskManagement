import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../api/api.config';
import {
  AdminUser,
  AdminUserListQuery,
  AdminUserListResponse,
  UpdateUserRoleRequest,
  UpdateUserStatusRequest
} from './admin-user.models';

const API = `${API_BASE_URL}/admin/users`;

@Injectable({ providedIn: 'root' })
export class AdminUserService {
  private readonly http = inject(HttpClient);

  getUsers(query: AdminUserListQuery): Observable<AdminUserListResponse> {
    let params = new HttpParams()
      .set('pageNumber', query.pageNumber)
      .set('pageSize', query.pageSize)
      .set('sortBy', query.sortBy)
      .set('sortDescending', query.sortDescending);

    if (query.search?.trim()) {
      params = params.set('search', query.search.trim());
    }

    if (query.isActive !== undefined) {
      params = params.set('isActive', query.isActive);
    }

    if (query.role) {
      params = params.set('role', query.role);
    }

    return this.http.get<AdminUserListResponse>(API, { params });
  }

  updateStatus(userId: string, request: UpdateUserStatusRequest): Observable<AdminUser> {
    return this.http.patch<AdminUser>(
      `${API}/${encodeURIComponent(userId)}/status`,
      request
    );
  }

  updateRole(userId: string, request: UpdateUserRoleRequest): Observable<AdminUser> {
    return this.http.put<AdminUser>(
      `${API}/${encodeURIComponent(userId)}/role`,
      request
    );
  }
}
