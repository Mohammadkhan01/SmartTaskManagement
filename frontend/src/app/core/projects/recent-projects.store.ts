import { Injectable, signal } from '@angular/core';

export interface RecentProject {
  id: string;
  name: string;
}

const MAX_RECENT = 4;

/**
 * Projects the user opened during this session, newest first. Kept in memory only
 * and cleared on sign-out, so nothing about one user's projects outlives their session.
 */
@Injectable({ providedIn: 'root' })
export class RecentProjectsStore {
  private readonly items = signal<RecentProject[]>([]);

  readonly projects = this.items.asReadonly();

  remember(project: RecentProject): void {
    this.items.update(list =>
      [{ id: project.id, name: project.name }, ...list.filter(item => item.id !== project.id)].slice(0, MAX_RECENT));
  }

  forget(projectId: string): void {
    this.items.update(list => list.filter(item => item.id !== projectId));
  }

  clear(): void {
    this.items.set([]);
  }

  nameOf(projectId: string): string | undefined {
    return this.items().find(item => item.id === projectId)?.name;
  }
}
