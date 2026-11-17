import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { describeHttpError, validationMessage } from '../../core/api/api-error';
import { AI_DESCRIPTION_MAX_LENGTH, AiService } from '../../core/ai/ai.service';
import { ProjectMember, ProjectStatus } from '../../core/projects/project.models';
import { ProjectService } from '../../core/projects/project.service';
import {
  CreateTaskRequest,
  TASK_DESCRIPTION_MAX_LENGTH,
  TASK_PRIORITY_LABELS,
  TASK_TITLE_MAX_LENGTH,
  Task,
  TaskPriority
} from '../../core/tasks/task.models';
import { TaskService } from '../../core/tasks/task.service';

@Component({
  selector: 'app-task-form',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <main class="page">
      <a class="back" [routerLink]="backLink()">← {{ isEdit ? 'Back to task' : 'Back to tasks' }}</a>

      <section class="panel">
        <p class="eyebrow">{{ isEdit ? 'EDIT TASK' : 'NEW TASK' }}</p>
        <h1>{{ isEdit ? 'Edit task' : 'Create a task' }}</h1>

        @if (loading()) {
          <p role="status">Loading…</p>
        } @else if (loadError()) {
          <p class="error" role="alert">{{ loadError() }}</p>
        } @else {
          <form [formGroup]="form" (ngSubmit)="save()">
            <label for="task-title">Title</label>
            <input id="task-title" type="text" formControlName="title"
              [maxlength]="titleMax" autocomplete="off">
            @if (form.controls.title.touched && form.controls.title.invalid) {
              <small class="error">Enter a title (maximum {{ titleMax }} characters).</small>
            }

            <div class="label-row">
              <label for="task-description">Description</label>
              <button type="button" class="ai-button"
                [disabled]="enhancing() || !canEnhance()"
                [attr.aria-busy]="enhancing()"
                (click)="enhanceDescription()">
                {{ enhancing() ? 'Enhancing…' : '✨ Enhance with AI' }}
              </button>
            </div>
            <textarea id="task-description" rows="6" formControlName="description"
              [maxlength]="descriptionMax"></textarea>
            <small class="muted">{{ descriptionLength() }} / {{ descriptionMax }} characters</small>
            @if (form.controls.description.invalid) {
              <small class="error">The description cannot exceed {{ descriptionMax }} characters.</small>
            }

            @if (aiError()) {
              <p class="error" role="alert">{{ aiError() }}</p>
            }

            @if (suggestion(); as text) {
              <section class="suggestion" aria-live="polite" aria-label="AI suggestion">
                <div class="suggestion-heading">
                  <strong>AI suggestion</strong>
                  <span class="muted">Review before using. Nothing is saved until you save the task.</span>
                </div>
                <p>{{ text }}</p>
                @if (text.length > descriptionMax) {
                  <small class="error">
                    This suggestion is {{ text.length }} characters, which exceeds the {{ descriptionMax }}-character
                    task limit. Discard it or shorten your original description and try again.
                  </small>
                }
                <div class="form-actions">
                  <button type="button" [disabled]="text.length > descriptionMax" (click)="acceptSuggestion()">
                    Use suggestion
                  </button>
                  <button type="button" class="secondary" (click)="discardSuggestion()">Discard</button>
                </div>
              </section>
            }

            <div class="grid">
              <div class="field">
                <label for="task-priority">Priority</label>
                <select id="task-priority" formControlName="priority">
                  @for (option of priorityOptions; track option.value) {
                    <option [ngValue]="option.value">{{ option.label }}</option>
                  }
                </select>
              </div>
              <div class="field">
                <label for="task-due">Due date</label>
                <input id="task-due" type="date" formControlName="dueDate">
              </div>
              <div class="field">
                <label for="task-assignee">Assignee</label>
                @if (membersAvailable()) {
                  <select id="task-assignee" formControlName="assignedToUserId">
                    <option value="">Unassigned</option>
                    @for (member of members(); track member.userId) {
                      <option [value]="member.userId">{{ member.displayName }} ({{ member.email }})</option>
                    }
                  </select>
                } @else {
                  <p class="muted">Project members are only visible to project managers.</p>
                }
              </div>
            </div>
            <small class="muted">
              @if (canAssign()) {
                Assigning a task requires the project to be Active.
              } @else {
                This project is not Active, so new tasks stay unassigned until the project is activated.
              }
            </small>

            @if (saveError()) {
              <p class="error" role="alert">{{ saveError() }}</p>
            }

            <div class="form-actions">
              <button type="submit" [disabled]="saving() || form.invalid">
                {{ saving() ? 'Saving…' : (isEdit ? 'Save changes' : 'Create task') }}
              </button>
              <a class="button secondary" [routerLink]="backLink()">Cancel</a>
            </div>
          </form>
        }
      </section>
    </main>
  `,
  styles: [`
    :host { display:block; }
    .page { max-width:860px; margin:auto; padding:24px 24px 48px; }
    .back { color:#1d4ed8; text-decoration:none; font-size:14px; }
    .panel { margin-top:18px; padding:28px; background:white; border:1px solid #e5eaf1; border-radius:12px; }
    .eyebrow { margin:0; color:#64748b; font-size:11px; font-weight:700; letter-spacing:1.2px; }
    h1 { margin:8px 0 20px; font-size:28px; }
    form { display:grid; gap:9px; }
    label { margin-top:10px; color:#475569; font-size:13px; font-weight:600; }
    .label-row { display:flex; justify-content:space-between; align-items:flex-end; gap:12px; }
    input, textarea, select { width:100%; border:1px solid #cbd5e1; border-radius:7px; padding:10px 12px; font:inherit; color:#17243b; background:white; }
    input:focus, textarea:focus, select:focus { outline:2px solid #93c5fd; border-color:#2563eb; }
    textarea { resize:vertical; }
    .grid { display:grid; grid-template-columns:repeat(3, minmax(0,1fr)); gap:16px; }
    .field { display:grid; gap:7px; align-content:start; }
    button, a.button { border:0; border-radius:8px; padding:10px 16px; background:#1d4ed8; color:white; cursor:pointer; text-decoration:none; font-size:14px; }
    button:disabled { opacity:.5; cursor:not-allowed; }
    .secondary, a.button.secondary { background:#eef2f7; color:#334155; }
    .ai-button { padding:7px 12px; background:#ede9fe; color:#5b21b6; font-size:13px; font-weight:600; }
    .form-actions { display:flex; gap:10px; margin-top:10px; flex-wrap:wrap; }
    .suggestion { margin-top:6px; padding:16px; border:1px solid #ddd6fe; border-radius:10px; background:#faf5ff; }
    .suggestion-heading { display:flex; flex-wrap:wrap; gap:8px 14px; align-items:baseline; }
    .suggestion p { margin:12px 0 4px; white-space:pre-wrap; line-height:1.55; color:#1e293b; }
    .error { color:#b91c1c; font-size:13px; }
    .muted { color:#64748b; font-size:12px; margin:0; }
    @media(max-width:700px) { .grid { grid-template-columns:1fr; } .page { padding:16px; } }
  `]
})
export class TaskFormComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly tasks = inject(TaskService);
  private readonly projects = inject(ProjectService);
  private readonly ai = inject(AiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly titleMax = TASK_TITLE_MAX_LENGTH;
  readonly descriptionMax = TASK_DESCRIPTION_MAX_LENGTH;
  readonly priorityOptions = Object.entries(TASK_PRIORITY_LABELS).map(([value, label]) => ({
    value: Number(value) as TaskPriority,
    label
  }));

  private readonly taskId = this.route.snapshot.paramMap.get('taskId');
  readonly isEdit = !!this.taskId;
  private projectId = this.route.snapshot.paramMap.get('id') ?? '';
  private existing: Task | null = null;

  readonly loading = signal(true);
  readonly loadError = signal('');
  readonly saving = signal(false);
  readonly saveError = signal('');
  readonly members = signal<ProjectMember[]>([]);
  readonly membersAvailable = signal(false);
  readonly projectStatus = signal<ProjectStatus | null>(null);
  readonly canAssign = computed(() => this.projectStatus() === ProjectStatus.Active);

  readonly enhancing = signal(false);
  readonly suggestion = signal<string | null>(null);
  readonly aiError = signal('');
  private readonly descriptionValue = signal('');

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(TASK_TITLE_MAX_LENGTH)]],
    description: ['', [Validators.maxLength(TASK_DESCRIPTION_MAX_LENGTH)]],
    priority: [TaskPriority.Medium, Validators.required],
    dueDate: [''],
    assignedToUserId: ['']
  });

  readonly descriptionLength = computed(() => this.descriptionValue().length);
  readonly canEnhance = computed(() => {
    const text = this.descriptionValue().trim();
    return text.length > 0 && text.length <= AI_DESCRIPTION_MAX_LENGTH;
  });

  constructor() {
    this.form.controls.description.valueChanges.pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(value => this.descriptionValue.set(value));

    if (this.taskId) {
      this.loadTask(this.taskId);
    } else if (this.projectId) {
      this.loadMembers();
      this.loading.set(false);
    } else {
      this.loading.set(false);
      this.loadError.set('A valid project ID is required.');
    }
  }

  backLink(): string[] {
    if (this.taskId) return ['/tasks', this.taskId];
    return this.projectId ? ['/projects', this.projectId, 'tasks'] : ['/projects'];
  }

  enhanceDescription(): void {
    if (this.enhancing() || !this.canEnhance()) return;

    const title = this.form.controls.title.value.trim();
    this.enhancing.set(true);
    this.aiError.set('');
    this.suggestion.set(null);

    this.ai.enhanceTaskDescription({
      title: title || null,
      description: this.form.controls.description.value.trim()
    }).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: response => {
        this.enhancing.set(false);
        const text = response?.enhancedDescription?.trim();
        if (text) {
          this.suggestion.set(text);
        } else {
          this.aiError.set('The AI service returned an empty suggestion. Your description was not changed.');
        }
      },
      error: (error: unknown) => {
        this.enhancing.set(false);
        this.aiError.set(this.aiErrorMessage(error));
      }
    });
  }

  acceptSuggestion(): void {
    const text = this.suggestion();
    if (!text || text.length > TASK_DESCRIPTION_MAX_LENGTH) return;
    this.form.controls.description.setValue(text);
    this.form.controls.description.markAsDirty();
    this.suggestion.set(null);
  }

  discardSuggestion(): void {
    this.suggestion.set(null);
  }

  save(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const title = value.title.trim();
    if (!title) {
      this.form.controls.title.setErrors({ required: true });
      this.form.controls.title.markAsTouched();
      return;
    }

    const selectedAssignee = (value.assignedToUserId || '').trim() || null;
    let assignedToUserId: string | null = null;
    if (this.membersAvailable()) {
      assignedToUserId = this.canAssign()
        ? selectedAssignee
        : (this.existing?.assignedToUserId ?? null);
    } else {
      assignedToUserId = this.existing?.assignedToUserId ?? null;
    }

    const request: CreateTaskRequest = {
      title,
      description: value.description.trim() || null,
      priority: Number(value.priority) as TaskPriority,
      dueDateUtc: value.dueDate ? toUtcNoon(value.dueDate) : null,
      assignedToUserId
    };

    this.saving.set(true);
    this.saveError.set('');

    const request$ = this.existing
      ? this.tasks.updateTask(this.existing.id, { ...request, rowVersion: this.existing.rowVersion })
      : this.tasks.createTask(this.projectId, request);

    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: task => {
        this.saving.set(false);
        void this.router.navigate(['/tasks', task.id]);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.saveError.set(describeHttpError(error, {
          badRequest: 'Some task details are invalid.',
          forbidden: 'You do not have permission to save this task.',
          notFound: 'The project or task was not found, or you do not have permission to manage its tasks.',
          conflict: 'This task was changed by someone else. Reload the latest version before editing again.',
          fallback: 'Could not save the task. Please try again.'
        }));
      }
    });
  }

  private loadTask(taskId: string): void {
    this.tasks.getTask(taskId).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: task => {
        this.existing = task;
        this.projectId = task.projectId;
        this.form.reset({
          title: task.title,
          description: task.description ?? '',
          priority: task.priority,
          dueDate: task.dueDateUtc ? task.dueDateUtc.slice(0, 10) : '',
          assignedToUserId: task.assignedToUserId ?? ''
        });
        this.loading.set(false);
        this.loadMembers();
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.loadError.set(describeHttpError(error, {
          notFound: 'Task not found or you do not have access to it.',
          fallback: 'Could not load the task. Please try again.'
        }));
      }
    });
  }

  private loadMembers(): void {
    this.projects.getProject(this.projectId).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: project => this.applyProjectStatus(project.status),
      error: () => this.projectStatus.set(null)
    });

    this.projects.getMembers(this.projectId).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: members => {
        this.members.set(members);
        this.membersAvailable.set(true);
      },
      error: () => this.membersAvailable.set(false)
    });
  }

  private applyProjectStatus(status: ProjectStatus): void {
    this.projectStatus.set(status);
    const control = this.form.controls.assignedToUserId;
    if (status === ProjectStatus.Active) {
      control.enable({ emitEvent: false });
      return;
    }

    // Creating against a non-Active project must not send an assignee.
    if (!this.isEdit) {
      control.setValue('', { emitEvent: false });
    }
    control.disable({ emitEvent: false });
  }

  private aiErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      switch (error.status) {
        case 0:
          return 'Cannot reach the server for AI enhancement. Your description was not changed.';
        case 400:
          return validationMessage(error) ?? 'The description cannot be enhanced as entered.';
        case 404:
        case 405:
        case 501:
          return 'AI enhancement is not available on this server yet.';
        case 429:
          return 'Too many AI requests. Wait a moment and try again.';
        case 502:
        case 503:
        case 504:
          return 'The AI service is unavailable or timed out. Try again later; your description was not changed.';
      }
    }
    return 'AI enhancement failed. Your description was not changed.';
  }
}

/** Date inputs yield a calendar date; noon UTC keeps that date stable across time zones. */
function toUtcNoon(date: string): string {
  return `${date}T12:00:00Z`;
}
