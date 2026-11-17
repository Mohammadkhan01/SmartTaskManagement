import { TaskStatus } from '../core/tasks/task.models';
import { dueState } from './due-date';

describe('dueState', () => {
  const now = new Date(2026, 10, 17, 9, 30);

  it('is none without a due date', () => {
    expect(dueState(null, TaskStatus.ToDo, now)).toBe('none');
  });

  it.each([
    ['2026-11-16T12:00:00Z', 'overdue'],
    ['2026-11-17T12:00:00', 'today'],
    ['2026-11-18T12:00:00Z', 'scheduled']
  ])('classifies %s as %s for open tasks', (due, expected) => {
    expect(dueState(due, TaskStatus.InProgress, now)).toBe(expected);
  });

  it.each([TaskStatus.Completed, TaskStatus.Cancelled])('never flags closed tasks (status %s)', status => {
    expect(dueState('2026-01-01T12:00:00Z', status, now)).toBe('scheduled');
  });
});
