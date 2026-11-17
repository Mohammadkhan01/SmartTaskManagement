import { Injectable, signal } from '@angular/core';

/** App-wide banner for cross-cutting problems such as the API being unreachable. */
@Injectable({ providedIn: 'root' })
export class AppNoticeStore {
  private readonly messageState = signal<string | null>(null);

  readonly message = this.messageState.asReadonly();

  show(message: string): void {
    this.messageState.set(message);
  }

  clear(): void {
    if (this.messageState() !== null) {
      this.messageState.set(null);
    }
  }
}
