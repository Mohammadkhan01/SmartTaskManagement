import { DatePipe } from '@angular/common';
import { UtcDatePipe } from '../../shared/pipes/utc-date.pipe';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { validationMessage } from '../../core/api/api-error';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  Project,
  ProjectMember,
  ProjectMemberRole,
  PROJECT_STATUS_LABELS,
  ProjectStatus,
  UpdateProjectRequest
} from '../../core/projects/project.models';
import { ProjectService } from '../../core/projects/project.service';
import { RecentProjectsStore } from '../../core/projects/recent-projects.store';
import { AvatarComponent } from '../../shared/ui/avatar.component';
import { IconComponent } from '../../shared/ui/icon.component';

@Component({
  selector: 'app-project-details',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [DatePipe],
  imports: [UtcDatePipe, RouterLink, ReactiveFormsModule, AvatarComponent, IconComponent],
  template: `
    <main class="page">
      <a class="back-link" routerLink="/projects"><app-icon name="arrowLeft" [size]="16" /> Back to projects</a>

      @if (loading()) {
        <div class="panel loading-rows" role="status">
          <span class="sr-only">Loading project…</span>
          <span class="skeleton title-skeleton" aria-hidden="true"></span>
          <span class="skeleton" aria-hidden="true"></span>
          <span class="skeleton" aria-hidden="true"></span>
        </div>
      } @else if (error()) {
        <section class="panel">
          <div class="empty-state">
            <span class="empty-state-icon tone-danger"><app-icon name="alert" /></span>
            <p class="error" role="alert">{{ error() }}</p>
            <button type="button" class="btn btn-primary" (click)="load()">Try again</button>
          </div>
        </section>
      } @else if (project(); as data) {
        <header class="page-header">
          <div class="title-block">
            <app-avatar [name]="data.name" size="xl" [square]="true" />
            <div class="title-text">
              <div class="title-row">
                <h1>{{ data.name }}</h1>
                <span [class]="'pill status project-status-' + data.status">{{ statusLabel(data.status) }}</span>
              </div>
              <p class="page-subtitle">Created {{ data.createdAtUtc | utcDate:'mediumDate' }}</p>
            </div>
          </div>
          <div class="page-actions">
            <a class="btn btn-primary" [routerLink]="['/projects', data.id, 'tasks']">
              <app-icon name="listChecks" [size]="16" /> View tasks
            </a>
            <button type="button" class="btn btn-secondary" [attr.aria-expanded]="editing()" (click)="toggleEdit()">
              <app-icon name="pencil" [size]="16" />
              {{ editing() ? 'Cancel edit' : 'Edit project' }}
            </button>
            <button type="button" class="btn btn-danger-ghost" [disabled]="deleting()" (click)="deleteProject()">
              <app-icon name="trash" [size]="16" />
              {{ deleting() ? 'Deleting…' : 'Delete project' }}
            </button>
          </div>
        </header>

        <div class="detail-grid">
          <div class="stack">
            <section class="panel" aria-labelledby="overview-title">
              <h2 id="overview-title" class="section-title">{{ editing() ? 'Edit project' : 'Overview' }}</h2>
              @if (editing()) {
                <form class="form" [formGroup]="editForm" (ngSubmit)="save()">
                  <div class="field">
                    <label for="name">Project name</label>
                    <input id="name" type="text" formControlName="name" maxlength="150">
                    @if (editForm.controls.name.touched && editForm.controls.name.invalid) {
                      <small class="field-error">A project name is required (maximum 150 characters).</small>
                    }
                  </div>
                  <div class="field">
                    <label for="description">Description</label>
                    <textarea id="description" formControlName="description" rows="5" maxlength="2000"></textarea>
                  </div>
                  <div class="field">
                    <label for="status">Status</label>
                    <select id="status" formControlName="status" aria-describedby="status-hint">
                      @for (option of statusOptions; track option.value) {
                        <option [ngValue]="option.value">{{ option.label }}</option>
                      }
                    </select>
                    <small id="status-hint" class="field-hint">Tasks can only be assigned while the project is Active.</small>
                  </div>
                  @if (saveError()) {
                    <p class="alert error" role="alert">{{ saveError() }}</p>
                  }
                  <div class="form-actions">
                    <button type="submit" class="btn btn-primary" [disabled]="saving() || editForm.invalid">
                      {{ saving() ? 'Saving…' : 'Save changes' }}
                    </button>
                    <button type="button" class="btn btn-secondary" (click)="toggleEdit()">Cancel</button>
                  </div>
                </form>
              } @else {
                <p class="description">{{ data.description || 'No description provided.' }}</p>
                @if (data.status !== activeStatus) {
                  <p class="hint">This project is {{ statusLabel(data.status) }}. Set it to Active to assign tasks.</p>
                }
                @if (saveError()) {
                  <p class="alert error" role="alert">{{ saveError() }}</p>
                }
              }
            </section>

            @if (membersAllowed()) {
              <section class="panel" aria-labelledby="members-title">
                <div class="panel-header">
                  <div>
                    <h2 id="members-title">Project members</h2>
                    <p class="panel-meta">{{ members().length }} {{ members().length === 1 ? 'member' : 'members' }}</p>
                  </div>
                </div>
                @if (membersLoading()) {
                  <p class="muted" role="status">Loading members…</p>
                } @else {
                  @if (memberError()) {
                    <p class="alert error" role="alert">{{ memberError() }}</p>
                  }
                  @if (members().length === 0) {
                    <p class="muted">No members found.</p>
                  } @else {
                    <ul class="member-list" role="list">
                      @for (member of members(); track member.userId) {
                        <li class="member">
                          <app-avatar [name]="member.displayName" />
                          <div class="member-info">
                            <strong>{{ member.displayName }}</strong>
                            <span>{{ member.email }}</span>
                          </div>
                          <span class="pill" [class.tone-primary]="member.projectRole === managerRole"
                            [class.tone-neutral]="member.projectRole !== managerRole">{{ roleLabel(member.projectRole) }}</span>
                          <button type="button" class="btn btn-danger-ghost btn-sm"
                            [disabled]="removingUserId() === member.userId"
                            (click)="removeMember(member)">
                            {{ removingUserId() === member.userId ? 'Removing…' : 'Remove' }}
                            <span class="sr-only">{{ member.displayName }}</span>
                          </button>
                        </li>
                      }
                    </ul>
                  }
                  <form class="member-form" [formGroup]="memberForm" (ngSubmit)="addMember()">
                    <div class="field">
                      <label for="memberUserId">Add member by user ID</label>
                      <div class="inline-input">
                        <input id="memberUserId" type="text" formControlName="userId"
                          placeholder="Enter the user's ID" aria-describedby="member-hint">
                        <button type="submit" class="btn btn-primary" [disabled]="addingMember() || memberForm.invalid">
                          <app-icon name="userPlus" [size]="16" />
                          {{ addingMember() ? 'Adding…' : 'Add member' }}
                        </button>
                      </div>
                      @if (memberForm.controls.userId.touched && memberForm.controls.userId.invalid) {
                        <small class="field-error">A user ID is required.</small>
                      }
                      <small id="member-hint" class="field-hint">User IDs are listed on the administrator's Users page.</small>
                    </div>
                  </form>
                }
              </section>
            } @else if (membersChecked()) {
              <section class="panel" aria-labelledby="members-title">
                <h2 id="members-title" class="section-title">Project members</h2>
                <p class="muted">You do not have permission to manage this project's members.</p>
              </section>
            }
          </div>

          <aside class="stack" aria-label="Project summary">
            <section class="panel" aria-labelledby="tasks-title">
              <h2 id="tasks-title" class="section-title">Tasks</h2>
              <p class="muted">Create, assign and track this project's work from its task list.</p>
              <a class="btn btn-secondary btn-block open-tasks" [routerLink]="['/projects', data.id, 'tasks']">
                Open task list <app-icon name="chevronRight" [size]="16" />
              </a>
            </section>
            <section class="panel" aria-labelledby="details-title">
              <h2 id="details-title" class="section-title">Details</h2>
              <dl class="detail-list">
                <div><dt>Created</dt><dd>{{ data.createdAtUtc | utcDate:'medium' }}</dd></div>
                <div><dt>Project ID</dt><dd><code>{{ data.id }}</code></dd></div>
              </dl>
            </section>
          </aside>
        </div>
      }
    </main>
  `,
  styles: [`
    :host { display:block; }
    .title-skeleton { width:40%; height:28px; }
    .title-block { display:flex; align-items:center; gap:16px; min-width:0; }
    .title-text { min-width:0; }
    .title-row { display:flex; flex-wrap:wrap; align-items:center; gap:8px 12px; }
    .title-row h1 { overflow-wrap:anywhere; }
    .detail-grid { display:grid; grid-template-columns:minmax(0,1fr) 320px; align-items:start; gap:20px; }
    .section-title { margin-bottom:14px; }
    .description { max-width:70ch; color:var(--c-text-2); line-height:1.65; white-space:pre-wrap; }
    .description ~ .hint, .description ~ .alert { margin-top:16px; }
    .member-list { margin:0 -24px; padding:0; list-style:none; border-top:1px solid var(--c-line); }
    .member { display:flex; align-items:center; gap:12px; padding:12px 24px; border-bottom:1px solid var(--c-line); }
    .member-info { display:grid; flex:1; min-width:0; }
    .member-info span { color:var(--c-muted); font-size:var(--text-sm); overflow-wrap:anywhere; }
    .member-form { margin-top:20px; }
    .inline-input { display:flex; gap:8px; }
    .open-tasks { margin-top:16px; }
    .detail-list code { color:var(--c-text-2); }
    @media (max-width:960px) { .detail-grid { grid-template-columns:minmax(0,1fr); } }
    @media (max-width:640px) {
      .title-block app-avatar { display:none; }
      .member-list { margin:0 -16px; }
      .member { flex-wrap:wrap; padding:12px 16px; }
      .inline-input { flex-direction:column; }
    }
  `]
})
export class ProjectDetailsComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(ProjectService);
  private readonly recent = inject(RecentProjectsStore);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  readonly project = signal<Project | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly editing = signal(false);
  readonly saving = signal(false);
  readonly deleting = signal(false);
  readonly saveError = signal('');

  readonly members = signal<ProjectMember[]>([]);
  readonly membersLoading = signal(false);
  readonly membersAllowed = signal(false);
  readonly membersChecked = signal(false);
  readonly memberError = signal('');
  readonly addingMember = signal(false);
  readonly removingUserId = signal('');

  readonly editForm = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(150)]],
    description: ['', Validators.maxLength(2000)],
    status: [ProjectStatus.Planning, Validators.required]
  });

  readonly activeStatus = ProjectStatus.Active;
  readonly managerRole = ProjectMemberRole.Manager;
  readonly statusOptions = Object.entries(PROJECT_STATUS_LABELS).map(([value, label]) => ({
    value: Number(value) as ProjectStatus,
    label
  }));

  readonly memberForm = this.fb.group({
    userId: ['', Validators.required]
  });

  private readonly projectId = this.route.snapshot.paramMap.get('id');

  constructor() {
    this.load();
  }

  load(): void {
    if (!this.projectId) {
      this.loading.set(false);
      this.error.set('A valid project ID is required.');
      return;
    }

    this.loading.set(true);
    this.error.set('');

    this.service.getProject(this.projectId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: result => {
        this.project.set(result);
        this.recent.remember(result);
        this.loading.set(false);
        this.loadMembers();
      },
      error: (error: unknown) => {
        this.loading.set(false);
        if (error instanceof HttpErrorResponse && error.status === 404) {
          this.error.set('Project not found or you do not have access to it.');
        } else if (error instanceof HttpErrorResponse && error.status === 403) {
          this.error.set('You do not have permission to view this project.');
        } else {
          this.error.set('Could not load project details. Please try again.');
        }
      }
    });
  }

  private loadMembers(): void {
    if (!this.projectId) return;

    this.membersLoading.set(true);
    this.memberError.set('');
    this.membersChecked.set(false);

    this.service.getMembers(this.projectId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: result => {
        this.members.set(result);
        this.membersAllowed.set(true);
        this.membersChecked.set(true);
        this.membersLoading.set(false);
      },
      error: (error: unknown) => {
        this.membersLoading.set(false);
        this.membersAllowed.set(false);
        this.membersChecked.set(true);
        if (!(error instanceof HttpErrorResponse && error.status === 403)) {
          this.memberError.set(
            error instanceof HttpErrorResponse && error.status === 404
              ? 'Project or member list not found.'
              : 'Could not load project members.'
          );
        }
      }
    });
  }

  toggleEdit(): void {
    const current = this.project();
    if (!current) return;

    this.editing.update(value => !value);
    this.saveError.set('');

    if (this.editing()) {
      this.editForm.reset({
        name: current.name,
        description: current.description ?? '',
        status: current.status
      });
    }
  }

  save(): void {
    const current = this.project();
    if (!current || !this.projectId || this.editForm.invalid || this.saving()) return;

    const request: UpdateProjectRequest = {
      name: this.editForm.controls.name.value?.trim() ?? '',
      description: this.editForm.controls.description.value?.trim() || null,
      status: this.editForm.controls.status.value ?? current.status,
      rowVersion: current.rowVersion
    };

    if (!request.name) {
      this.editForm.controls.name.setErrors({ required: true });
      return;
    }

    this.saving.set(true);
    this.saveError.set('');

    this.service.updateProject(this.projectId, request).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: updated => {
        this.project.set(updated);
        this.recent.remember(updated);
        this.editing.set(false);
        this.saving.set(false);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        if (error instanceof HttpErrorResponse && error.status === 409) {
          this.saveError.set('This project was changed by someone else. Reload the latest version before editing again.');
        } else if (error instanceof HttpErrorResponse && error.status === 403) {
          this.saveError.set('You do not have permission to edit this project.');
        } else if (error instanceof HttpErrorResponse && error.status === 400) {
          this.saveError.set(validationMessage(error) ?? 'Some project details are invalid.');
        } else {
          this.saveError.set('Could not save changes. Please try again.');
        }
      }
    });
  }

  deleteProject(): void {
    if (!this.projectId || this.deleting()) return;
    if (!window.confirm('Delete this project? This action cannot be undone.')) return;

    const projectId = this.projectId;
    this.deleting.set(true);
    this.service.deleteProject(projectId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.recent.forget(projectId);
        void this.router.navigate(['/projects']);
      },
      error: (error: unknown) => {
        this.deleting.set(false);
        this.saveError.set(
          error instanceof HttpErrorResponse && error.status === 403
            ? 'You do not have permission to delete this project.'
            : 'Could not delete the project. It may have changed or may still contain dependent data.'
        );
      }
    });
  }

  addMember(): void {
    if (!this.projectId || this.memberForm.invalid || this.addingMember()) return;

    const userId = this.memberForm.controls.userId.value?.trim() ?? '';
    if (!userId) return;

    this.addingMember.set(true);
    this.memberError.set('');

    this.service.addMember(this.projectId, { userId }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: member => {
        this.members.update(current => [...current, member]);
        this.memberForm.reset();
        this.addingMember.set(false);
      },
      error: (error: unknown) => {
        this.addingMember.set(false);
        this.memberError.set(this.memberErrorMessage(error, 'Could not add this member.'));
      }
    });
  }

  removeMember(member: ProjectMember): void {
    if (!this.projectId || this.removingUserId()) return;
    if (!window.confirm(`Remove ${member.displayName} from this project?`)) return;

    this.removingUserId.set(member.userId);
    this.memberError.set('');

    this.service.removeMember(this.projectId, member.userId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.members.update(current => current.filter(item => item.userId !== member.userId));
        this.removingUserId.set('');
      },
      error: (error: unknown) => {
        this.removingUserId.set('');
        this.memberError.set(this.memberErrorMessage(error, 'Could not remove this member.'));
      }
    });
  }

  private memberErrorMessage(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 400) return error.error?.detail ?? error.error?.title ?? 'Invalid user ID or this user is already a project member.';
      if (error.status === 403) return 'You do not have permission to manage project members.';
      if (error.status === 404) return 'Project or user not found.';
    }
    return fallback;
  }

  statusLabel(status: ProjectStatus): string {
    return PROJECT_STATUS_LABELS[status] ?? 'Unknown';
  }

  roleLabel(role: ProjectMemberRole): string {
    return role === ProjectMemberRole.Manager ? 'Manager' : 'Member';
  }
}
