import { Route, Routes } from '@angular/router';
import { routes } from './app.routes';
import { adminGuard } from './core/guards/admin.guard';
import { authGuard } from './core/guards/auth.guard';
import { AdminUsersComponent } from './features/admin/admin-users.component';
import { LoginComponent } from './features/auth/login.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { ProjectDetailsComponent } from './features/projects/project-details.component';
import { ProjectsComponent } from './features/projects/projects.component';
import { TaskDetailsComponent } from './features/tasks/task-details.component';
import { TaskFormComponent } from './features/tasks/task-form.component';
import { TaskListComponent } from './features/tasks/task-list.component';
import { AppShellComponent } from './layouts/app-shell.component';

const shell = routes.find(route => route.path === '' && route.children)!;
const child = (path: string): Route => shell.children!.find(route => route.path === path)!;

async function loaded(route: Route): Promise<unknown> {
  expect(route.loadComponent).toBeTypeOf('function');
  expect(route.component).toBeUndefined();
  return route.loadComponent!();
}

describe('app routes', () => {
  it('lazy-loads the public login page without guards', async () => {
    const login = routes.find(route => route.path === 'login')!;
    expect(login.canActivate).toBeUndefined();
    expect(await loaded(login)).toBe(LoginComponent);
  });

  it('protects the lazy-loaded shell with authGuard', async () => {
    expect(shell.canActivate).toEqual([authGuard]);
    expect(await loaded(shell)).toBe(AppShellComponent);
  });

  const featureRoutes: [string, unknown][] = [
    ['dashboard', DashboardComponent],
    ['projects', ProjectsComponent],
    ['projects/:id', ProjectDetailsComponent],
    ['projects/:id/tasks', TaskListComponent],
    ['projects/:id/tasks/new', TaskFormComponent],
    ['tasks/:taskId', TaskDetailsComponent],
    ['tasks/:taskId/edit', TaskFormComponent],
    ['admin/users', AdminUsersComponent]
  ];

  it.each(featureRoutes)('lazy-loads %s inside the protected shell', async (path, component) => {
    const route = child(path);
    expect(route).toBeDefined();
    expect(await loaded(route)).toBe(component);
  });

  it('only the admin route adds adminGuard', () => {
    expect(child('admin/users').canActivate).toEqual([adminGuard]);
    const others = shell.children!.filter(route => route.path !== 'admin/users');
    expect(others.every(route => !route.canActivate)).toBe(true);
  });

  it('redirects the empty and unknown paths to the dashboard', () => {
    expect(child('')).toMatchObject({ pathMatch: 'full', redirectTo: 'dashboard' });
    expect(routes.at(-1)).toMatchObject({ path: '**', redirectTo: 'dashboard' });
  });

  it('has no eagerly-declared components anywhere in the tree', () => {
    const walk = (list: Routes): Route[] => list.flatMap(route => [route, ...walk(route.children ?? [])]);
    expect(walk(routes).filter(route => route.component)).toEqual([]);
  });
});

describe('responsive layouts', () => {
  const pages: [string, unknown][] = [
    ['LoginComponent', LoginComponent],
    ['AppShellComponent', AppShellComponent],
    ['DashboardComponent', DashboardComponent],
    ['ProjectsComponent', ProjectsComponent],
    ['ProjectDetailsComponent', ProjectDetailsComponent],
    ['TaskListComponent', TaskListComponent],
    ['TaskFormComponent', TaskFormComponent],
    ['TaskDetailsComponent', TaskDetailsComponent],
    ['AdminUsersComponent', AdminUsersComponent]
  ];

  it.each(pages)('%s declares a max-width media query for small screens', (_name, component) => {
    const styles = ((component as { ɵcmp?: { styles?: string[] } }).ɵcmp?.styles ?? []).join('\n');
    expect(styles).toMatch(/@media\s*\(\s*max-width\s*:\s*\d+px\s*\)/);
  });
});
