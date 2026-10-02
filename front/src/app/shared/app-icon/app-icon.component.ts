import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { LucideDynamicIcon } from '@lucide/angular';

@Component({
  selector: 'app-icon',
  imports: [LucideDynamicIcon],
  template: `
    <svg
      [lucideIcon]="name()"
      [size]="size()"
      [strokeWidth]="strokeWidth()"
      aria-hidden="true"
      focusable="false"
    ></svg>
  `,
  styles: [
    `
      :host {
        display: inline-grid;
        place-items: center;
        flex: 0 0 auto;
        line-height: 0;
      }

      svg {
        display: block;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppIconComponent {
  readonly name = input.required<string>();
  readonly size = input(20);
  readonly strokeWidth = input(1.8);
}
