import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { App } from './app';

@Component({ standalone: true, template: '<p class="stub-page">Stub page</p>' })
class StubPageComponent {}

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([{ path: 'stub', component: StubPageComponent }])]
    }).compileComponents();
  });

  it('creates the root component', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('hosts a router outlet that renders the routed page', async () => {
    const fixture = TestBed.createComponent(App);
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('router-outlet')).not.toBeNull();

    await TestBed.inject(Router).navigateByUrl('/stub');
    fixture.detectChanges();
    await fixture.whenStable();

    expect(element.querySelector('.stub-page')?.textContent).toContain('Stub page');
  });
});
