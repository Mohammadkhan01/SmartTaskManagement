import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../api/api.config';
import { AdminDashboard, DashboardSummary, DashboardTaskStatistics } from './dashboard.models';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);

  getSummary(): Observable<DashboardSummary> {
    return this.http.get<DashboardSummary>(`${API_BASE_URL}/dashboard/summary`);
  }

  getTaskStatistics(): Observable<DashboardTaskStatistics> {
    return this.http.get<DashboardTaskStatistics>(`${API_BASE_URL}/dashboard/task-statistics`);
  }

  getAdminDashboard(): Observable<AdminDashboard> {
    return this.http.get<AdminDashboard>(`${API_BASE_URL}/admin/dashboard`);
  }
}
