export const GLOBAL_ROLES = ['Admin', 'Project Manager', 'Team Member'] as const;

export type GlobalRole = (typeof GLOBAL_ROLES)[number];

export interface AdminUser {
  userId: string;
  displayName: string;
  email: string;
  isActive: boolean;
  roles: string[];
}

export interface AdminUserListQuery {
  pageNumber: number;
  pageSize: number;
  search?: string;
  isActive?: boolean;
  role?: GlobalRole;
  sortBy: 'email' | 'displayName' | 'isActive';
  sortDescending: boolean;
}

export interface AdminUserListResponse {
  items: AdminUser[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface UpdateUserStatusRequest {
  isActive: boolean;
}

export interface UpdateUserRoleRequest {
  role: GlobalRole;
}
