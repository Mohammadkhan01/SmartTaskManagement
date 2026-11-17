import { TaskPriority, TaskStatus } from '../tasks/task.models';

export interface DashboardTaskItem {
  id: string;
  projectId: string;
  title: string;
  status: TaskStatus;
  priority: TaskPriority;
  dueDateUtc: string | null;
}

export interface DashboardProjectTotals {
  totalProjects: number;
  planningProjects: number;
  activeProjects: number;
  completedProjects: number;
  onHoldProjects: number;
  archivedProjects: number;
}

export interface DashboardSummary {
  totalTasks: number;
  toDoTasks: number;
  inProgressTasks: number;
  completedTasks: number;
  cancelledTasks: number;
  overdueTasks: number;
  dueTodayTasks: number;
  completionPercentage: number;
  recentTasks: DashboardTaskItem[];
  projectTotals?: DashboardProjectTotals | null;
}

export interface AdminDashboard {
  projects: DashboardProjectTotals;
  totalTasks: number;
  toDoTasks: number;
  inProgressTasks: number;
  completedTasks: number;
  cancelledTasks: number;
  overdueTasks: number;
  completionPercentage: number;
  totalUsers: number;
  activeUsers: number;
  inactiveUsers: number;
  adminUsers: number;
  projectManagerUsers: number;
  teamMemberUsers: number;
}

export interface DashboardTaskStatistics {
  pendingTasks: number;
  lowPriorityTasks: number;
  mediumPriorityTasks: number;
  highPriorityTasks: number;
  criticalPriorityTasks: number;
  upcomingTasks: DashboardTaskItem[];
}
