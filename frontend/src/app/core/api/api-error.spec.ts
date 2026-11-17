import { HttpErrorResponse } from '@angular/common/http';
import { describeHttpError, validationMessage } from './api-error';

const httpError = (status: number, error: unknown = null) => new HttpErrorResponse({ status, error });

describe('validationMessage', () => {
  it('joins ValidationProblemDetails errors', () => {
    const error = httpError(400, { errors: { Title: ['Title is required.'], Email: ['Bad email.', ''] } });
    expect(validationMessage(error)).toBe('Title is required. Bad email.');
  });

  it('falls back to the ProblemDetails detail', () => {
    expect(validationMessage(httpError(400, { errors: {}, detail: 'Nope.' }))).toBe('Nope.');
  });

  it('returns null for bodies without messages', () => {
    expect(validationMessage(httpError(400, 'plain text'))).toBeNull();
    expect(validationMessage(httpError(400, null))).toBeNull();
    expect(validationMessage(httpError(400, { title: 'Bad Request' }))).toBeNull();
  });
});

describe('describeHttpError', () => {
  const messages = { fallback: 'Fallback.' };

  it('maps network errors', () => {
    expect(describeHttpError(httpError(0), messages)).toContain('Cannot reach the server');
  });

  it('prefers validation text for 400 then the badRequest message', () => {
    expect(describeHttpError(httpError(400, { detail: 'Too long.' }), messages)).toBe('Too long.');
    expect(describeHttpError(httpError(400), { ...messages, badRequest: 'Invalid.' })).toBe('Invalid.');
    expect(describeHttpError(httpError(400), messages)).toBe('Fallback.');
  });

  it('uses defaults or overrides for 403, 404 and 409', () => {
    expect(describeHttpError(httpError(403), messages)).toBe('You do not have permission to perform this action.');
    expect(describeHttpError(httpError(403), { ...messages, forbidden: 'No.' })).toBe('No.');
    expect(describeHttpError(httpError(404), messages)).toBe('Fallback.');
    expect(describeHttpError(httpError(404), { ...messages, notFound: 'Gone.' })).toBe('Gone.');
    expect(describeHttpError(httpError(409), messages)).toContain('changed by someone else');
    expect(describeHttpError(httpError(409), { ...messages, conflict: 'Conflict.' })).toBe('Conflict.');
  });

  it('falls back for other statuses and non-HTTP errors', () => {
    expect(describeHttpError(httpError(500), messages)).toBe('Fallback.');
    expect(describeHttpError(new Error('boom'), messages)).toBe('Fallback.');
  });
});
