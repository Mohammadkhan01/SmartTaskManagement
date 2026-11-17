import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../api/api.config';
import { AI_DESCRIPTION_MAX_LENGTH, AiService } from './ai.service';

describe('AiService', () => {
  let service: AiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('matches the backend task-description and AI input limit of 4000 characters', () => {
    expect(AI_DESCRIPTION_MAX_LENGTH).toBe(4000);
  });

  it('posts the title and description to the enhancement endpoint', () => {
    let enhanced: string | undefined;
    service.enhanceTaskDescription({ title: 'Fix login', description: 'login broke' })
      .subscribe(response => (enhanced = response.enhancedDescription));

    const req = http.expectOne(`${API_BASE_URL}/ai/enhance-task-description`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ title: 'Fix login', description: 'login broke' });
    req.flush({ enhancedDescription: 'Investigate and fix the login failure.' });

    expect(enhanced).toBe('Investigate and fix the login failure.');
  });

  it('propagates provider failures to the caller', () => {
    let status: number | undefined;
    service.enhanceTaskDescription({ title: null, description: 'x' })
      .subscribe({ error: error => (status = error.status) });

    http.expectOne(`${API_BASE_URL}/ai/enhance-task-description`)
      .flush(null, { status: 503, statusText: 'Service Unavailable' });

    expect(status).toBe(503);
  });
});
