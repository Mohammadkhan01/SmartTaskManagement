export enum ProjectStatus {
  Planning = 1,
  Active = 2,
  Completed = 3,
  OnHold = 4,
  Archived = 5
}

export enum ProjectMemberRole {
  Manager = 1,
  Member = 2
}

export interface Project {
  id: string;
  name: string;
  description: string | null;
  status: ProjectStatus;
  createdByUserId: string;
  createdAtUtc: string;
  rowVersion: string;
}

export interface CreateProjectRequest {
  name: string;
  description: string | null;
}

export interface UpdateProjectRequest {
  name: string;
  description: string | null;
  rowVersion: string;
}

export interface ProjectListQuery {
  pageNumber: number;
  pageSize: number;
  search?: string;
  status?: ProjectStatus;
  sortBy: 'createdAt' | 'updatedAt' | 'name' | 'status';
  sortDescending: boolean;
}

export interface ProjectListResponse {
  items: Project[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface ProjectMember {
  userId: string;
  displayName: string;
  email: string;
  projectRole: ProjectMemberRole;
}

export interface AddProjectMemberRequest {
  userId: string;
}
