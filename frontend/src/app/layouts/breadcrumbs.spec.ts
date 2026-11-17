import { buildBreadcrumbs } from './breadcrumbs';

describe('buildBreadcrumbs', () => {
  const names: Record<string, string> = { p1: 'Apollo' };
  const crumbs = (url: string) => buildBreadcrumbs(url, id => names[id]);

  it.each([
    ['/dashboard', [{ label: 'Dashboard' }]],
    ['/projects?page=2', [{ label: 'Projects' }]],
    ['/admin/users', [{ label: 'Administration' }, { label: 'Users' }]]
  ])('labels top-level page %s', (url, expected) => {
    expect(crumbs(url)).toEqual(expected);
  });

  it('uses the remembered project name and links back up the trail', () => {
    expect(crumbs('/projects/p1/tasks/new')).toEqual([
      { label: 'Projects', link: '/projects' },
      { label: 'Apollo', link: '/projects/p1' },
      { label: 'Tasks', link: '/projects/p1/tasks' },
      { label: 'New task' }
    ]);
  });

  it('falls back to a generic label for projects that have not been loaded yet', () => {
    expect(crumbs('/projects/p9')).toEqual([{ label: 'Projects', link: '/projects' }, { label: 'Project' }]);
  });

  it('describes task pages', () => {
    expect(crumbs('/tasks/t1')).toEqual([{ label: 'Projects', link: '/projects' }, { label: 'Task' }]);
    expect(crumbs('/tasks/t1/edit')).toEqual([
      { label: 'Projects', link: '/projects' },
      { label: 'Task', link: '/tasks/t1' },
      { label: 'Edit' }
    ]);
  });
});
