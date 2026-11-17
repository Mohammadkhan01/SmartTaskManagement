export enum TaskStatus {
  ToDo = 1,
  InProgress = 2,
  Completed = 3,
  Cancelled = 4
}

export enum TaskPriority {
  Low = 1,
  Medium = 2,
  High = 3,
  Critical = 4
}

export const TASK_STATUS_LABELS: Record<TaskStatus, string> = {
  [TaskStatus.ToDo]: 'To do',
  [TaskStatus.InProgress]: 'In progress',
  [TaskStatus.Completed]: 'Completed',
  [TaskStatus.Cancelled]: 'Cancelled'
};

export const TASK_PRIORITY_LABELS: Record<TaskPriority, string> = {
  [TaskPriority.Low]: 'Low',
  [TaskPriority.Medium]: 'Medium',
  [TaskPriority.High]: 'High',
  [TaskPriority.Critical]: 'Critical'
};

export const TASK_TITLE_MAX_LENGTH = 200;
export const TASK_DESCRIPTION_MAX_LENGTH = 4000;

export interface Task {
  id: string;
  projectId: string;
  title: string;
  description: string | null;
  status: TaskStatus;
  priority: TaskPriority;
  assignedToUserId: string | null;
  createdByUserId: string;
  dueDateUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  rowVersion: string;
}

export interface CreateTaskRequest {
  title: string;
  description: string | null;
  priority: TaskPriority;
  assignedToUserId: string | null;
  dueDateUtc: string | null;
}

export interface UpdateTaskRequest extends CreateTaskRequest {
  rowVersion: string;
}

export interface AssignTaskRequest {
  assignedToUserId: string;
  rowVersion: string;
}

export interface UpdateTaskStatusRequest {
  status: TaskStatus;
  rowVersion: string;
}

export interface TaskListQuery {
  pageNumber: number;
  pageSize: number;
  search?: string;
  status?: TaskStatus;
  priority?: TaskPriority;
  assignedToUserId?: string;
  sortBy: 'createdAt' | 'updatedAt' | 'dueDate' | 'title' | 'priority' | 'status';
  sortDescending: boolean;
}

export interface TaskListResponse {
  items: Task[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}
