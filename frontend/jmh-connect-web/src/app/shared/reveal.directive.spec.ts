import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { RevealDirective } from './reveal.directive';

@Component({ imports: [RevealDirective], template: '<p appReveal="120">hello</p>' })
class Host {}

describe('RevealDirective', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('reveals immediately when IntersectionObserver is unavailable', () => {
    vi.stubGlobal('IntersectionObserver', undefined);
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const el = fixture.nativeElement.querySelector('p') as HTMLElement;
    expect(el.classList.contains('reveal')).toBe(true);
    expect(el.classList.contains('revealed')).toBe(true);
  });

  it('waits for the element to intersect, applies the delay, then reveals once', () => {
    let callback: (entries: Array<{ isIntersecting: boolean }>) => void = () => undefined;
    const disconnect = vi.fn();
    vi.stubGlobal('IntersectionObserver', class {
      constructor(cb: typeof callback) { callback = cb; }
      observe() {}
      disconnect = disconnect;
    });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const el = fixture.nativeElement.querySelector('p') as HTMLElement;
    expect(el.classList.contains('revealed')).toBe(false);
    expect(el.style.getPropertyValue('--reveal-delay')).toBe('120ms');

    callback([{ isIntersecting: true }]);
    fixture.detectChanges();
    expect(el.classList.contains('revealed')).toBe(true);
    expect(disconnect).toHaveBeenCalled();
  });
});
