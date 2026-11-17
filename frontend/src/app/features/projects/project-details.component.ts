import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  Project,
  ProjectMember,
  ProjectMemberRole,
  ProjectStatus,
  UpdateProjectRequest
} from '../../core/projects/project.models';
import { ProjectService } from '../../core/projects/project.service';

@Component({
  selector: 'app-project-details',
  standalone: true,
  imports: [DatePipe, RouterLink, ReactiveFormsModule],
  template: `
    <main class="page">
      <header><a routerLink="/projects">← Back to projects</a></header>

      @if (loading()) {
        <p role="status">Loading project…</p>
      } @else if (error()) {
        <section class="panel">
          <p class="error" role="alert">{{ error() }}</p>
          <button type="button" (click)="load()">Try again</button>
        </section>
      } @else if (project(); as data) {
        <section class="panel">
          <div class="heading">
            <div>
              <p class="eyebrow">PROJECT DETAILS</p>
              <h1>{{ data.name }}</h1>
              <span class="status">{{ statusLabel(data.status) }}</span>
            </div>
            <div class="actions">
              <button type="button" class="secondary" (click)="toggleEdit()">
                {{ editing() ? 'Cancel edit' : 'Edit project' }}
              </button>
              <button type="button" class="danger" [disabled]="deleting()" (click)="deleteProject()">
                {{ deleting() ? 'Deleting…' : 'Delete project' }}
              </button>
            </div>
          </div>

          @if (editing()) {
            <form [formGroup]="editForm" (ngSubmit)="save()">
              <label for="name">Project name</label>
              <input id="name" type="text" formControlName="name" maxlength="150">
              @if (editForm.controls.name.touched && editForm.controls.name.invalid) {
                <small class="error">A project name is required (maximum 150 characters).</small>
              }
              <label for="description">Description</label>
              <textarea id="description" formControlName="description" rows="5" maxlength="2000"></textarea>
              @if (saveError()) {
                <p class="error" role="alert">{{ saveError() }}</p>
              }
              <button type="submit" [disabled]="saving() || editForm.invalid">
                {{ saving() ? 'Saving…' : 'Save changes' }}
              </button>
            </form>
          } @else {
            <h2>Description</h2>
            <p class="description">{{ data.description || 'No description provided.' }}</p>
          }

          <dl>
            <div><dt>Created</dt><dd>{{ data.createdAtUtc | date:'medium' }}</dd></div>
            <div><dt>Project ID</dt><dd>{{ data.id }}</dd></div>
          </dl>
        </section>

        @if (membersAllowed()) {
          <section class="panel">
            <h2>Project members</h2>
            @if (membersLoading()) {
              <p role="status">Loading members…</p>
            } @else {
              @if (memberError()) {
                <p class="error" role="alert">{{ memberError() }}</p>
              }
              @if (members().length === 0) {
                <p class="muted">No members found.</p>
              } @else {
                <div class="member-list">
                  @for (member of members(); track member.userId) {
                    <div class="member">
                      <div class="member-info">
                        <strong>{{ member.displayName }}</strong>
                        <span>{{ member.email }}</span>
                        <small>{{ roleLabel(member.projectRole) }}</small>
                      </div>
                      <button
                        type="button"
                        class="danger"
                        [disabled]="removingUserId() === member.userId"
                        (click)="removeMember(member)">
                        {{ removingUserId() === member.userId ? 'Removing…' : 'Remove' }}
                      </button>
                    </div>
                  }
                </div>
              }
              <form [formGroup]="memberForm" (ngSubmit)="addMember()">
                <label for="memberUserId">Add member by user ID</label>
                <input id="memberUserId" type="text" formControlName="userId"
                  placeholder="Enter the user's ID">
                @if (memberForm.controls.userId.touched && memberForm.controls.userId.invalid) {
                  <small class="error">A user ID is required.</small>
                }
                <button type="submit" [disabled]="addingMember() || memberForm.invalid">
                  {{ addingMember() ? 'Adding…' : 'Add member' }}
                </button>
              </form>
            }
          </section>
        } @else if (membersChecked()) {
          <section class="panel">
            <h2>Project members</h2>
            <p class="muted">You do not have permission to manage this project's members.</p>
          </section>
        }
      }
    </main>
  `,
  styles: [`
    :host { display:block; min-height:100vh; background:#f4f6fa; }
    .page { max-width:900px; margin:auto; padding:32px 24px; }
    a { color:#1d4ed8; text-decoration:none; font-size:14px; }
    .panel { margin-top:24px; padding:28px; background:white; border:1px solid #e5eaf1; border-radius:12px; }
    .heading { display:flex; justify-content:space-between; align-items:flex-start; gap:20px; }
    .eyebrow { color:#64748b; font-size:11px; font-weight:700; letter-spacing:1.2px; }
    h1 { margin:8px 0 14px; font-size:30px; }
    h2 { margin:12px 0 20px; font-size:19px; }
    .status { display:inline-block; padding:5px 10px; border-radius:20px; background:#dbeafe; color:#1d4ed8; font-size:12px; }
    .description { color:#475569; line-height:1.6; white-space:pre-wrap; }
    .actions { display:flex; gap:8px; flex-wrap:wrap; }
    form { display:grid; gap:10px; margin-top:24px; }
    label { margin-top:10px; font-size:13px; font-weight:600; }
    input, textarea { width:100%; padding:11px; border:1px solid #cbd5e1; border-radius:7px; font:inherit; }
    dl { margin-top:28px; }
    dl div { display:grid; grid-template-columns:140px 1fr; gap:12px; padding:12px 0; border-top:1px solid #edf0f4; }
    dt { color:#64748b; font-size:13px; }
    dd { margin:0; font-size:13px; overflow-wrap:anywhere; }
    .error { color:#b91c1c; }
    .muted { color:#64748b; font-size:14px; }
    button { border:0; border-radius:8px; padding:10px 16px; background:#1d4ed8; color:white; cursor:pointer; }
    button:disabled { opacity:.55; cursor:not-allowed; }
    button.secondary { background:#e2e8f0; color:#1e293b; }
    button.danger { background:#b91c1c; }
    .member-list { display:grid; }
    .member { display:flex; justify-content:space-between; align-items:center; gap:16px; padding:14px 0; border-top:1px solid #edf0f4; }
    .member-info { display:grid; gap:4px; min-width:0; }
    .member-info strong { font-size:14px; }
    .member-info span, .member-info small { color:#64748b; font-size:12px; overflow-wrap:anywhere; }
    @media (max-width:650px) {
      .heading { flex-direction:column; }
      .member { align-items:flex-start; }
      dl div { grid-template-columns:100px 1fr; }
    }
  `]
})
export class ProjectDetailsComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(ProjectService);
  private readonly fb = inject(FormBuilder);

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
    description: ['', Validators.maxLength(2000)]
  });

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

    this.service.getProject(this.projectId).subscribe({
      next: result => {
        this.project.set(result);
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

    this.service.getMembers(this.projectId).subscribe({
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
        description: current.description ?? ''
      });
    }
  }

  save(): void {
    const current = this.project();
    if (!current || !this.projectId || this.editForm.invalid || this.saving()) return;

    const request: UpdateProjectRequest = {
      name: this.editForm.controls.name.value?.trim() ?? '',
      description: this.editForm.controls.description.value?.trim() || null,
      rowVersion: current.rowVersion
    };

    if (!request.name) {
      this.editForm.controls.name.setErrors({ required: true });
      return;
    }

    this.saving.set(true);
    this.saveError.set('');

    this.service.updateProject(this.projectId, request).subscribe({
      next: updated => {
        this.project.set(updated);
        this.editing.set(false);
        this.saving.set(false);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        if (error instanceof HttpErrorResponse && error.status === 409) {
          this.saveError.set('This project was changed by someone else. Reload the latest version before editing again.');
        } else if (error instanceof HttpErrorResponse && error.status === 403) {
          this.saveError.set('You do not have permission to edit this project.');
        } else {
          this.saveError.set('Could not save changes. Please try again.');
        }
      }
    });
  }

  deleteProject(): void {
    if (!this.projectId || this.deleting()) return;
    if (!window.confirm('Delete this project? This action cannot be undone.')) return;

    this.deleting.set(true);
    this.service.deleteProject(this.projectId).subscribe({
      next: () => this.router.navigate(['/projects']),
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

    this.service.addMember(this.projectId, { userId }).subscribe({
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

    this.service.removeMember(this.projectId, member.userId).subscribe({
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
    return ({
      [ProjectStatus.Planning]: 'Planning',
      [ProjectStatus.Active]: 'Active',
      [ProjectStatus.Completed]: 'Completed',
      [ProjectStatus.OnHold]: 'On hold',
      [ProjectStatus.Archived]: 'Archived'
    } as Record<number, string>)[status] ?? 'Unknown';
  }

  roleLabel(role: ProjectMemberRole): string {
    return role === ProjectMemberRole.Manager ? 'Manager' : 'Member';
  }
}
