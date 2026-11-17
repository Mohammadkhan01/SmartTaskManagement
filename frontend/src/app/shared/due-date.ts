import { TaskStatus } from '../core/tasks/task.models';

/**
 * How a due date should be presented. Closed tasks are never flagged, and the
 * comparison is by calendar day because due dates are stored as UTC-noon dates.
 */
export type DueState = 'none' | 'scheduled' | 'today' | 'overdue';

export function dueState(dueDateUtc: string | null | undefined, status: TaskStatus, now = new Date()): DueState {
  if (!dueDateUtc) return 'none';
  if (status === TaskStatus.Completed || status === TaskStatus.Cancelled) return 'scheduled';

  const dueDay = dueDateUtc.slice(0, 10);
  const today = [
    now.getFullYear(),
    String(now.getMonth() + 1).padStart(2, '0'),
    String(now.getDate()).padStart(2, '0')
  ].join('-');

  if (dueDay < today) return 'overdue';
  return dueDay === today ? 'today' : 'scheduled';
}
