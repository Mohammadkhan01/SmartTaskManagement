import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../api/api.config';

export const AI_DESCRIPTION_MAX_LENGTH = 4000;

export interface EnhanceTaskDescriptionRequest {
  title: string | null;
  description: string;
}

export interface EnhanceTaskDescriptionResponse {
  enhancedDescription: string;
}

@Injectable({ providedIn: 'root' })
export class AiService {
  private readonly http = inject(HttpClient);

  enhanceTaskDescription(
    request: EnhanceTaskDescriptionRequest
  ): Observable<EnhanceTaskDescriptionResponse> {
    return this.http.post<EnhanceTaskDescriptionResponse>(
      `${API_BASE_URL}/ai/enhance-task-description`,
      request
    );
  }
}
