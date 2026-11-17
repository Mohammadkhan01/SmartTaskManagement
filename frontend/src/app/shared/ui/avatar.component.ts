import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

const TONES = 6;

/** First letters of the first two words, e.g. "Pat Lee" → "PL". */
export function initials(name: string | null | undefined): string {
  const words = (name ?? '').trim().split(/\s+/).filter(Boolean);
  if (words.length === 0) return '?';
  const letters = words.length === 1 ? words[0].slice(0, 2) : words[0][0] + words[1][0];
  return letters.toUpperCase();
}

/** Stable colour slot for a name so the same person or project keeps the same avatar colour. */
export function avatarTone(seed: string | null | undefined): number {
  let hash = 0;
  for (const char of seed ?? '') {
    hash = (hash * 31 + char.charCodeAt(0)) | 0;
  }
  return Math.abs(hash) % TONES;
}

/** Decorative initials badge; the name itself must be rendered next to it. */
@Component({
  selector: 'app-avatar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    'aria-hidden': 'true',
    class: 'avatar',
    '[class.avatar-sm]': 'size() === "sm"',
    '[class.avatar-lg]': 'size() === "lg"',
    '[class.avatar-xl]': 'size() === "xl"',
    '[class.avatar-square]': 'square()',
    '[attr.data-tone]': 'tone()'
  },
  template: '{{ letters() }}',
  styles: [`
    :host {
      display:inline-grid; place-items:center; flex-shrink:0; width:32px; height:32px;
      border-radius:50%; font-size:12px; font-weight:700; letter-spacing:.02em; line-height:1;
      background:var(--avatar-bg); color:var(--avatar-fg); user-select:none;
    }
    :host(.avatar-sm) { width:26px; height:26px; font-size:10.5px; }
    :host(.avatar-lg) { width:44px; height:44px; font-size:15px; }
    :host(.avatar-xl) { width:56px; height:56px; font-size:19px; }
    :host(.avatar-square) { border-radius:10px; }
    :host(.avatar-square.avatar-xl) { border-radius:14px; }
    :host([data-tone='0']) { --avatar-bg:#e4e7fb; --avatar-fg:#2d3ba8; }
    :host([data-tone='1']) { --avatar-bg:#dff1ea; --avatar-fg:#0f6545; }
    :host([data-tone='2']) { --avatar-bg:#fdecdf; --avatar-fg:#a3461a; }
    :host([data-tone='3']) { --avatar-bg:#e1effa; --avatar-fg:#145d91; }
    :host([data-tone='4']) { --avatar-bg:#f4e4f6; --avatar-fg:#8a2e93; }
    :host([data-tone='5']) { --avatar-bg:#fbf0d2; --avatar-fg:#7a5205; }
  `]
})
export class AvatarComponent {
  readonly name = input<string | null | undefined>('');
  readonly size = input<'sm' | 'md' | 'lg' | 'xl'>('md');
  readonly square = input(false);

  readonly letters = computed(() => initials(this.name()));
  readonly tone = computed(() => avatarTone(this.name()));
}
