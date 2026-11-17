export interface Breadcrumb {
  label: string;
  /** Omitted for the current page. */
  link?: string;
}

/**
 * Builds the top-bar trail for an app URL. Project names come from the recently
 * viewed list when available; otherwise a generic label keeps the trail stable.
 */
export function buildBreadcrumbs(url: string, projectName: (id: string) => string | undefined): Breadcrumb[] {
  const segments = url.split(/[?#]/)[0].split('/').filter(Boolean).map(decodeURIComponent);
  const [section, id, child, leaf] = segments;

  switch (section) {
    case 'dashboard':
      return [{ label: 'Dashboard' }];
    case 'admin':
      return [{ label: 'Administration' }, { label: 'Users' }];
    case 'tasks':
      if (!id) break;
      return child === 'edit'
        ? [{ label: 'Projects', link: '/projects' }, { label: 'Task', link: `/tasks/${id}` }, { label: 'Edit' }]
        : [{ label: 'Projects', link: '/projects' }, { label: 'Task' }];
    case 'projects': {
      if (!id) return [{ label: 'Projects' }];
      const project = projectName(id) ?? 'Project';
      if (child !== 'tasks') return [{ label: 'Projects', link: '/projects' }, { label: project }];
      const trail: Breadcrumb[] = [
        { label: 'Projects', link: '/projects' },
        { label: project, link: `/projects/${id}` }
      ];
      return leaf === 'new'
        ? [...trail, { label: 'Tasks', link: `/projects/${id}/tasks` }, { label: 'New task' }]
        : [...trail, { label: 'Tasks' }];
    }
  }
  return [{ label: 'Dashboard' }];
}
