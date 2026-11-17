import { Routes } from '@angular/router';
import { adminGuard } from './core/guards/admin.guard';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent)
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layouts/app-shell.component').then(m => m.AppShellComponent),
    children: [
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'projects',
        loadComponent: () => import('./features/projects/projects.component').then(m => m.ProjectsComponent)
      },
      {
        path: 'projects/:id',
        loadComponent: () => import('./features/projects/project-details.component').then(m => m.ProjectDetailsComponent)
      },
      {
        path: 'projects/:id/tasks',
        loadComponent: () => import('./features/tasks/task-list.component').then(m => m.TaskListComponent)
      },
      {
        path: 'projects/:id/tasks/new',
        loadComponent: () => import('./features/tasks/task-form.component').then(m => m.TaskFormComponent)
      },
      {
        path: 'tasks/:taskId',
        loadComponent: () => import('./features/tasks/task-details.component').then(m => m.TaskDetailsComponent)
      },
      {
        path: 'tasks/:taskId/edit',
        loadComponent: () => import('./features/tasks/task-form.component').then(m => m.TaskFormComponent)
      },
      {
        path: 'admin/users',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/admin/admin-users.component').then(m => m.AdminUsersComponent)
      },
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' }
    ]
  },
  { path: '**', redirectTo: 'dashboard' }
];
