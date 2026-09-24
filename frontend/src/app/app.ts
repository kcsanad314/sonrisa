import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnDestroy, OnInit, isDevMode, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';

type Channel = 'Email' | 'Slack';
type DeliveryStatus = 'Pending' | 'Sent' | 'Failed';

interface Alert {
  id: number;
  name: string;
  minimumMagnitude: number;
  isEnabled: boolean;
  channels: Channel[];
}

interface Earthquake {
  id: number;
  title: string;
  place: string;
  magnitude: number;
  occurredAtUtc: string;
  sourceUrl: string;
  isDemo: boolean;
}

interface Delivery {
  id: number;
  earthquakeTitle: string;
  alertName: string;
  channel: Channel;
  status: DeliveryStatus;
  attemptCount: number;
  lastAttemptAtUtc: string | null;
  lastError: string | null;
}

@Component({
  imports: [CommonModule, FormsModule],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App implements OnInit, OnDestroy {
  readonly alerts = signal<Alert[]>([]);
  readonly earthquakes = signal<Earthquake[]>([]);
  readonly deliveries = signal<Delivery[]>([]);
  readonly demoAvailable = isDevMode();
  readonly message = signal('');
  readonly error = signal('');
  readonly busy = signal(false);

  editingId: number | null = null;
  name = '';
  minimumMagnitude = 4.5;
  emailSelected = true;
  slackSelected = false;
  demoMagnitude = 5.0;
  private refreshTimer?: ReturnType<typeof setInterval>;

  constructor(private readonly http: HttpClient) {}

  ngOnInit(): void {
    this.refresh();
    this.refreshTimer = setInterval(() => this.refresh(), 15000);
  }

  ngOnDestroy(): void {
    if (this.refreshTimer) clearInterval(this.refreshTimer);
  }

  refresh(): void {
    forkJoin({
      alerts: this.http.get<Alert[]>('/api/alerts'),
      earthquakes: this.http.get<Earthquake[]>('/api/earthquakes'),
      deliveries: this.http.get<Delivery[]>('/api/deliveries'),
    }).subscribe({
      next: (data) => {
        this.alerts.set(data.alerts);
        this.earthquakes.set(data.earthquakes);
        this.deliveries.set(data.deliveries);
        this.error.set('');
      },
      error: () => this.error.set('Could not load data. Check that the API is running on localhost:5003.'),
    });
  }

  edit(alert: Alert): void {
    this.editingId = alert.id;
    this.name = alert.name;
    this.minimumMagnitude = alert.minimumMagnitude;
    this.emailSelected = alert.channels.includes('Email');
    this.slackSelected = alert.channels.includes('Slack');
    this.message.set('');
    this.error.set('');
  }

  clearForm(): void {
    this.editingId = null;
    this.name = '';
    this.minimumMagnitude = 4.5;
    this.emailSelected = true;
    this.slackSelected = false;
  }

  saveAlert(): void {
    const channels: Channel[] = [];
    if (this.emailSelected) channels.push('Email');
    if (this.slackSelected) channels.push('Slack');
    if (!this.name.trim() || !Number.isFinite(this.minimumMagnitude) ||
        this.minimumMagnitude < 2.5 || channels.length === 0) {
      this.error.set('Enter a name, magnitude of at least 2.5, and one or both channels.');
      return;
    }

    const body = { name: this.name.trim(), minimumMagnitude: this.minimumMagnitude, channels };
    const request = this.editingId === null
      ? this.http.post<Alert>('/api/alerts', body)
      : this.http.put<Alert>(`/api/alerts/${this.editingId}`, body);
    this.busy.set(true);
    request.subscribe({
      next: () => {
        this.busy.set(false);
        this.message.set(this.editingId === null ? 'Alert created.' : 'Alert updated.');
        this.clearForm();
        this.refresh();
      },
      error: (response) => this.fail(response),
    });
  }

  toggle(alert: Alert): void {
    this.busy.set(true);
    this.http.patch<Alert>(`/api/alerts/${alert.id}/enabled`, { isEnabled: !alert.isEnabled })
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.message.set(`Alert ${alert.isEnabled ? 'disabled' : 'enabled'}.`);
          this.refresh();
        },
        error: (response) => this.fail(response),
      });
  }

  createDemoEarthquake(): void {
    if (!Number.isFinite(this.demoMagnitude) || this.demoMagnitude < 2.5) {
      this.error.set('Demo magnitude must be at least 2.5.');
      return;
    }
    this.busy.set(true);
    this.http.post<{ earthquakeId: number; deliveryCount: number }>(
      '/api/demo/earthquake', { magnitude: this.demoMagnitude })
      .subscribe({
        next: (result) => {
          this.busy.set(false);
          this.message.set(`Demo earthquake created with ${result.deliveryCount} pending deliveries. Results refresh every 15 seconds.`);
          this.refresh();
        },
        error: (response) => this.fail(response),
      });
  }

  private fail(response: { error?: { error?: string } }): void {
    this.busy.set(false);
    this.error.set(response.error?.error ?? 'The request failed. Please try again.');
  }
}
