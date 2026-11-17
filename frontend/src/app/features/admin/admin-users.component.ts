import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/api-error';
import {
  AdminUser,
  AdminUserListQuery,
  AdminUserListResponse,
  GLOBAL_ROLES,
  GlobalRole
} from '../../core/admin/admin-user.models';
import { AdminUserService } from '../../core/admin/admin-user.service';
import { SessionStore } from '../../core/auth/session-store';

@Component({
  selector: 'app-admin-users',
  standalone: true,
  template: `
    <main class="page">
      <section class="heading">
        <div>
          <p class="eyebrow">ADMINISTRATION</p>
          <h1>User management</h1>
          <p class="subtitle">Activate or deactivate accounts and change global roles.</p>
        </div>
      </section>

      <section class="panel">
        <div class="filters">
          <label class="search">
            <span>Search users</span>
            <input type="search" placeholder="Search by name or email"
              [value]="searchText()" (input)="onSearch($event)">
          </label>
          <label>
            <span>Role</span>
            <select (change)="onRoleFilter($event)">
              <option value="">All roles</option>
              @for (role of roles; track role) {
                <option [value]="role" [selected]="role === roleFilter()">{{ role }}</option>
              }
            </select>
          </label>
          <label>
            <span>Account status</span>
            <select (change)="onActiveFilter($event)">
              <option value="">All accounts</option>
              <option value="true" [selected]="activeFilter() === 'true'">Active</option>
              <option value="false" [selected]="activeFilter() === 'false'">Inactive</option>
            </select>
          </label>
          <label>
            <span>Sort by</span>
            <select (change)="onSortChange($event)">
              <option value="email" [selected]="sortBy() === 'email'">Email</option>
              <option value="displayName" [selected]="sortBy() === 'displayName'">Name</option>
              <option value="isActive" [selected]="sortBy() === 'isActive'">Status</option>
            </select>
          </label>
        </div>

        @if (actionError()) {
          <p class="error" role="alert">{{ actionError() }}</p>
        }
        @if (actionSuccess()) {
          <p class="success" role="status">{{ actionSuccess() }}</p>
        }

        @if (loading()) {
          <p class="message" role="status">Loading users…</p>
        } @else if (error()) {
          <div class="message">
            <p class="error" role="alert">{{ error() }}</p>
            <button type="button" class="secondary" (click)="loadUsers()">Try again</button>
          </div>
        } @else if (result(); as data) {
          @if (data.items.length === 0) {
            <div class="empty">
              <h2>No users found</h2>
              <p>Try changing your search or filters.</p>
            </div>
          } @else {
            <div class="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th scope="col">User</th>
                    <th scope="col">Status</th>
                    <th scope="col">Role</th>
                    <th scope="col">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  @for (user of data.items; track user.userId) {
                    <tr>
                      <td>
                        <strong>{{ user.displayName || '(no name)' }}{{ isSelf(user) ? ' (you)' : '' }}</strong>
                        <span class="muted">{{ user.email }}</span>
                        <code class="user-id" title="User ID (used when adding project members)">{{ user.userId }}</code>
                      </td>
                      <td>
                        <span class="badge" [class.inactive]="!user.isActive">
                          {{ user.isActive ? 'Active' : 'Inactive' }}
                        </span>
                      </td>
                      <td>
                        <label class="sr-only" [for]="'role-' + user.userId">Role for {{ user.email }}</label>
                        <select [id]="'role-' + user.userId" [disabled]="busyUserId() === user.userId"
                          (change)="changeRole(user, $event)">
                          @if (!primaryRole(user)) {
                            <option value="" disabled selected>No role</option>
                          }
                          @for (role of roles; track role) {
                            <option [value]="role" [selected]="role === primaryRole(user)">{{ role }}</option>
                          }
                        </select>
                      </td>
                      <td>
                        <button type="button" [class.danger]="user.isActive" [class.secondary]="!user.isActive"
                          [disabled]="busyUserId() === user.userId || (isSelf(user) && user.isActive)"
                          [attr.title]="isSelf(user) && user.isActive ? 'You cannot deactivate your own account.' : null"
                          (click)="toggleActive(user)">
                          {{ busyUserId() === user.userId ? 'Saving…' : (user.isActive ? 'Deactivate' : 'Activate') }}
                        </button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }

          <footer class="pagination">
            <span>
              {{ data.totalCount }} user(s) · Page {{ data.pageNumber }} of {{ data.totalPages || 1 }}
            </span>
            <div>
              <button type="button" class="secondary" (click)="changePage(data.pageNumber - 1)"
                [disabled]="!data.hasPreviousPage">Previous</button>
              <button type="button" class="secondary" (click)="changePage(data.pageNumber + 1)"
                [disabled]="!data.hasNextPage">Next</button>
            </div>
          </footer>
        }
      </section>
    </main>
  `,
  styles: [`
    :host { display:block; }
    .page { max-width:1180px; margin:auto; padding:24px 28px 48px; }
    .heading { margin:10px 0 24px; }
    h1 { margin:5px 0 6px; font-size:32px; }
    h2 { margin:0 0 8px; font-size:20px; }
    .eyebrow { margin:0; color:#64748b; font-size:11px; font-weight:700; letter-spacing:1.3px; }
    .subtitle { margin:0; color:#64748b; font-size:14px; }
    button { border:0; border-radius:8px; padding:9px 15px; background:#1d4ed8; color:white; cursor:pointer; }
    button:disabled { opacity:.5; cursor:not-allowed; }
    .secondary { background:#eef2f7; color:#334155; }
    .danger { background:#b91c1c; color:white; }
    .panel { background:white; border:1px solid #e5eaf1; border-radius:12px; padding:22px; }
    label { color:#475569; font-size:13px; font-weight:600; }
    input, select { width:100%; border:1px solid #cbd5e1; border-radius:7px; padding:9px 12px; color:#17243b; background:white; font:inherit; }
    .filters { display:grid; grid-template-columns:minmax(220px,1fr) repeat(3, 170px); gap:16px; margin-bottom:22px; }
    .filters label { display:grid; gap:7px; }
    .message { padding:22px 0; color:#64748b; }
    .empty { text-align:center; padding:42px 16px; color:#64748b; }
    .empty h2 { color:#17243b; }
    .error { color:#b91c1c; font-size:13px; }
    .success { color:#166534; font-size:13px; }
    .table-wrap { overflow-x:auto; }
    table { width:100%; border-collapse:collapse; text-align:left; }
    th { color:#64748b; font-size:12px; font-weight:600; }
    th,td { padding:14px 12px; border-bottom:1px solid #edf0f4; font-size:13px; }
    td strong, td .muted { display:block; }
    td select { max-width:200px; }
    .muted { color:#64748b; margin-top:4px; }
    .user-id { display:block; margin-top:4px; color:#94a3b8; font-size:11px; user-select:all; }
    .badge { display:inline-block; padding:5px 9px; border-radius:20px; background:#dcfce7; color:#166534; font-size:12px; }
    .badge.inactive { background:#f1f5f9; color:#64748b; }
    .pagination { display:flex; justify-content:space-between; align-items:center; gap:16px; padding-top:20px; color:#64748b; font-size:13px; }
    .pagination div { display:flex; gap:8px; }
    .sr-only { position:absolute; width:1px; height:1px; overflow:hidden; clip:rect(0 0 0 0); }
    @media(max-width:900px) {
      .page { padding:16px; }
      .filters { grid-template-columns:1fr; }
      .pagination { align-items:flex-start; flex-direction:column; }
    }
  `]
})
export class AdminUsersComponent {
  private readonly admin = inject(AdminUserService);
  private readonly session = inject(SessionStore);
  private readonly destroyRef = inject(DestroyRef);

  readonly roles = GLOBAL_ROLES;
  readonly result = signal<AdminUserListResponse | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly actionError = signal('');
  readonly actionSuccess = signal('');
  readonly busyUserId = signal('');

  readonly searchText = signal('');
  readonly roleFilter = signal('');
  readonly activeFilter = signal('');
  readonly sortBy = signal<AdminUserListQuery['sortBy']>('email');
  readonly pageNumber = signal(1);

  private readonly searchChanges = new Subject<string>();

  constructor() {
    this.searchChanges.pipe(
      debounceTime(350),
      distinctUntilChanged(),
      switchMap(search => {
        this.pageNumber.set(1);
        this.loading.set(true);
        this.error.set('');
        return this.admin.getUsers(this.query(search)).pipe(
          catchError(error => {
            this.handleLoadError(error);
            return of(null);
          })
        );
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(result => this.applyResult(result));

    this.loadUsers();
  }

  onSearch(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchText.set(value);
    this.searchChanges.next(value);
  }

  onRoleFilter(event: Event): void {
    this.roleFilter.set((event.target as HTMLSelectElement).value);
    this.reload();
  }

  onActiveFilter(event: Event): void {
    this.activeFilter.set((event.target as HTMLSelectElement).value);
    this.reload();
  }

  onSortChange(event: Event): void {
    this.sortBy.set((event.target as HTMLSelectElement).value as AdminUserListQuery['sortBy']);
    this.reload();
  }

  changePage(page: number): void {
    this.pageNumber.set(page);
    this.loadUsers();
  }

  loadUsers(): void {
    this.loading.set(true);
    this.error.set('');
    this.admin.getUsers(this.query()).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: result => this.applyResult(result),
      error: error => this.handleLoadError(error)
    });
  }

  isSelf(user: AdminUser): boolean {
    return user.userId === this.session.user()?.userId;
  }

  primaryRole(user: AdminUser): GlobalRole | '' {
    return GLOBAL_ROLES.find(role => user.roles.includes(role)) ?? '';
  }

  toggleActive(user: AdminUser): void {
    if (this.busyUserId()) return;
    const isActive = !user.isActive;
    const verb = isActive ? 'Activate' : 'Deactivate';
    if (!window.confirm(`${verb} the account for ${user.email}?`)) return;

    this.busyUserId.set(user.userId);
    this.clearMessages();

    this.admin.updateStatus(user.userId, { isActive }).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: updated => {
        this.replaceUser(updated);
        this.actionSuccess.set(`${updated.email} is now ${updated.isActive ? 'active' : 'inactive'}.`);
      },
      error: error => this.handleActionError(error, 'Could not change the account status.')
    });
  }

  changeRole(user: AdminUser, event: Event): void {
    const select = event.target as HTMLSelectElement;
    const role = select.value as GlobalRole;
    const previous = this.primaryRole(user);
    if (!role || role === previous || this.busyUserId()) return;

    if (!window.confirm(`Change ${user.email}'s role to ${role}?`)) {
      select.value = previous;
      return;
    }

    this.busyUserId.set(user.userId);
    this.clearMessages();

    this.admin.updateRole(user.userId, { role }).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: updated => {
        this.replaceUser(updated);
        this.actionSuccess.set(`${updated.email} is now ${role}.`);
      },
      error: error => {
        select.value = previous;
        this.handleActionError(error, 'Could not change the role.');
      }
    });
  }

  private replaceUser(updated: AdminUser): void {
    this.busyUserId.set('');
    this.result.update(current => current && {
      ...current,
      items: current.items.map(item => item.userId === updated.userId ? updated : item)
    });
  }

  private handleActionError(error: unknown, fallback: string): void {
    this.busyUserId.set('');
    if (error instanceof HttpErrorResponse &&
        (error.status === 403 || error.status === 409) &&
        typeof error.error?.detail === 'string') {
      this.actionError.set(error.error.detail);
      return;
    }
    this.actionError.set(describeHttpError(error, {
      forbidden: 'This change is not permitted (for example, changing your own account).',
      notFound: 'That user no longer exists.',
      conflict: 'This change conflicts with the current state (for example, removing the last active admin).',
      fallback
    }));
  }

  private clearMessages(): void {
    this.actionError.set('');
    this.actionSuccess.set('');
  }

  private reload(): void {
    this.pageNumber.set(1);
    this.loadUsers();
  }

  private query(search = this.searchText()): AdminUserListQuery {
    const role = this.roleFilter();
    const active = this.activeFilter();
    return {
      pageNumber: this.pageNumber(),
      pageSize: 20,
      search,
      ...(role ? { role: role as GlobalRole } : {}),
      ...(active ? { isActive: active === 'true' } : {}),
      sortBy: this.sortBy(),
      sortDescending: false
    };
  }

  private applyResult(result: AdminUserListResponse | null): void {
    if (result) {
      this.result.set(result);
    }
    this.loading.set(false);
  }

  private handleLoadError(error: unknown): void {
    this.loading.set(false);
    this.error.set(describeHttpError(error, {
      forbidden: 'Only administrators can manage users.',
      fallback: 'Could not load users. Please try again.'
    }));
  }
}
