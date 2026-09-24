import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { App } from './app';

describe('App', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function flushDashboard(): void {
    http.expectOne('/api/alerts').flush([]);
    http.expectOne('/api/earthquakes').flush([]);
    http.expectOne('/api/deliveries').flush([]);
  }

  it('renders alerts and delivery errors', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    http.expectOne('/api/alerts').flush([{
      id: 1, name: 'M4.5+', minimumMagnitude: 4.5, isEnabled: true,
      channels: ['Email', 'Slack'],
    }]);
    http.expectOne('/api/earthquakes').flush([]);
    http.expectOne('/api/deliveries').flush([{
      id: 4, earthquakeTitle: 'Demo quake', alertName: 'M4.5+', channel: 'Email',
      status: 'Failed', attemptCount: 1, lastAttemptAtUtc: '2026-09-24T18:00:00Z',
      lastError: 'SMTP unavailable',
    }]);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('M4.5+');
    expect(text).toContain('Email, Slack');
    expect(text).toContain('Failed');
    expect(text).toContain('SMTP unavailable');
    fixture.destroy();
  });

  it('creates an alert with selected channels', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    flushDashboard();
    const app = fixture.componentInstance;
    app.name = 'Review alert';
    app.minimumMagnitude = 5;
    app.emailSelected = true;
    app.slackSelected = true;

    app.saveAlert();

    const request = http.expectOne('/api/alerts');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      name: 'Review alert', minimumMagnitude: 5, channels: ['Email', 'Slack'],
    });
    request.flush({ id: 1 });
    flushDashboard();
    expect(app.message()).toBe('Alert created.');
    fixture.destroy();
  });

  it('posts a fixture magnitude to the local demo endpoint', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    flushDashboard();
    const app = fixture.componentInstance;
    app.demoMagnitude = 5.2;

    app.createDemoEarthquake();

    const request = http.expectOne('/api/demo/earthquake');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ magnitude: 5.2 });
    request.flush({ earthquakeId: 3, deliveryCount: 2 });
    flushDashboard();
    expect(app.message()).toContain('2 pending deliveries');
    fixture.destroy();
  });
});
