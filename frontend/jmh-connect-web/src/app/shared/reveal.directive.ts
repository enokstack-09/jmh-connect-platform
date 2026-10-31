import { Directive, ElementRef, OnDestroy, OnInit, inject, input, numberAttribute, signal } from '@angular/core';

/**
 * Fades an element up the first time it scrolls into view.
 * Usage: <div appReveal> or <div [appReveal]="120"> (delay in ms).
 * Styles live in styles.scss (.reveal). Without IntersectionObserver the element is shown immediately.
 */
@Directive({
  selector: '[appReveal]',
  host: {
    class: 'reveal',
    '[class.revealed]': 'shown()',
    '[style.--reveal-delay]': 'delay() + "ms"',
  },
})
export class RevealDirective implements OnInit, OnDestroy {
  readonly delay = input(0, { alias: 'appReveal', transform: (value: unknown) => numberAttribute(value, 0) });

  protected readonly shown = signal(false);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private observer?: IntersectionObserver;

  ngOnInit(): void {
    if (typeof IntersectionObserver === 'undefined') {
      this.shown.set(true);
      return;
    }
    this.observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          this.shown.set(true);
          this.observer?.disconnect();
        }
      },
      { threshold: 0.12, rootMargin: '0px 0px -6% 0px' },
    );
    this.observer.observe(this.host.nativeElement);
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
  }
}
