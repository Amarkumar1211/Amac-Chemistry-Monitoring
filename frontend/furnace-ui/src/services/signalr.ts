import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { tokenStore } from './api';
import type { FurnaceClearedDto, FurnaceUpdateDto, ParsedSpectroResultDto, FurnaceStatusChangedDto } from '../types';

export type UpdateHandler = (update: FurnaceUpdateDto) => void;
export type ClearHandler = (cleared: FurnaceClearedDto) => void;
export type ConnectionStateHandler = (connected: boolean) => void;
export type PdfParsedHandler = (data: ParsedSpectroResultDto) => void;
export type FurnaceStatusHandler = (status: FurnaceStatusChangedDto) => void;

/**
 * Wraps the SignalR connection with JWT auth from the query string and
 * automatic reconnection with exponential backoff.
 */
export class RealtimeClient {
  private connection: HubConnection | null = null;
  private updateHandlers = new Set<UpdateHandler>();
  private clearHandlers = new Set<ClearHandler>();
  private stateHandlers = new Set<ConnectionStateHandler>();
  private systemStateHandlers = new Set<(isLive: boolean) => void>();
  private pdfParsedHandlers = new Set<PdfParsedHandler>();
  private furnaceStatusHandlers = new Set<FurnaceStatusHandler>();
  private joinedFurnaces = new Set<string>();
  private retries = 0;

  constructor() {}

  async connect(): Promise<void> {
    if (this.connection) return; // Prevent multiple connections during strict mode double-invocations

    const token = tokenStore.getToken();

    this.connection = new HubConnectionBuilder()
      .withUrl('/hubs/furnace', { accessTokenFactory: () => tokenStore.getToken() ?? '' })
      .configureLogging(LogLevel.Warning)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .build();

    this.connection.on('ReceiveUpdate', (update: FurnaceUpdateDto) => {
      this.updateHandlers.forEach((h) => h(update));
    });
    this.connection.on('ReceiveClear', (cleared: FurnaceClearedDto) => {
      this.clearHandlers.forEach((h) => h(cleared));
    });
    this.connection.on('ReceiveFurnaceStatusChanged', (status: FurnaceStatusChangedDto) => {
      this.furnaceStatusHandlers.forEach((h) => h(status));
    });
    this.connection.on('Joined', () => {
      // no-op: the client intentionally uses a silent join acknowledgement
    });
    this.connection.on('Left', () => {
      // no-op: the client intentionally uses a silent leave acknowledgement
    });
    this.connection.on('ReceiveError', (error: string) => {
      console.error('[SignalR] Server error:', error);
    });
    this.connection.on('SystemStateChanged', (isLive: boolean) => {
      this.systemStateHandlers.forEach((h) => h(isLive));
    });
    this.connection.on('ReceivePdfParsed', (data: ParsedSpectroResultDto) => {
      this.pdfParsedHandlers.forEach((h) => h(data));
    });

    this.connection.onreconnecting(() => {
      this.emitState(false);
    });
    this.connection.onreconnected(() => {
      this.retries = 0;
      this.emitState(true);
      this.rejoinAll();
    });
    this.connection.onclose(() => {
      this.emitState(false);
    });

    try {
      await this.connection.start();
      this.retries = 0;
      this.emitState(true);
      this.rejoinAll();
    } catch (err) {
      console.error('[SignalR] Failed to start:', err);
      this.emitState(false);
      throw err;
    }
  }

  async joinFurnace(furnaceNumber: string): Promise<void> {
    const key = furnaceNumber.trim().toUpperCase();
    if (this.connection?.state === HubConnectionState.Connected) {
      try {
        await this.connection.invoke('JoinFurnace', key);
        this.joinedFurnaces.add(key);
      } catch (err) {
        console.error(`[SignalR] Failed to join ${key}`, err);
      }
    } else {
      this.joinedFurnaces.add(key);
    }
  }

  async leaveFurnace(furnaceNumber: string): Promise<void> {
    const key = furnaceNumber.trim().toUpperCase();
    this.joinedFurnaces.delete(key);
    if (this.connection?.state === HubConnectionState.Connected) {
      try {
        await this.connection.invoke('LeaveFurnace', key);
      } catch (err) {
        console.warn('[SignalR] Leave failed', err);
      }
    }
  }

  onUpdate(handler: UpdateHandler): () => void {
    this.updateHandlers.add(handler);
    return () => this.updateHandlers.delete(handler);
  }

  onClear(handler: ClearHandler): () => void {
    this.clearHandlers.add(handler);
    return () => this.clearHandlers.delete(handler);
  }

  onState(handler: ConnectionStateHandler): () => void {
    this.stateHandlers.add(handler);
    return () => this.stateHandlers.delete(handler);
  }

  onSystemState(handler: (isLive: boolean) => void): () => void {
    this.systemStateHandlers.add(handler);
    return () => this.systemStateHandlers.delete(handler);
  }

  onPdfParsed(handler: PdfParsedHandler): () => void {
    this.pdfParsedHandlers.add(handler);
    return () => this.pdfParsedHandlers.delete(handler);
  }

  onFurnaceStatusChanged(handler: FurnaceStatusHandler): () => void {
    this.furnaceStatusHandlers.add(handler);
    return () => this.furnaceStatusHandlers.delete(handler);
  }

  async setSystemLive(isLive: boolean): Promise<void> {
    if (this.connection?.state === HubConnectionState.Connected) {
      await this.connection.invoke('SetSystemLive', isLive);
    }
  }

  isConnected(): boolean {
    return this.connection?.state === HubConnectionState.Connected;
  }

  async disconnect(): Promise<void> {
    this.joinedFurnaces.clear();
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
    }
    this.emitState(false);
  }

  private rejoinAll() {
    this.joinedFurnaces.forEach((furnace) => {
      this.connection
        ?.invoke('JoinFurnace', furnace)
        .catch((err) => console.error(`[SignalR] Rejoin ${furnace} failed`, err));
    });
  }

  private emitState(connected: boolean) {
    this.stateHandlers.forEach((h) => h(connected));
  }
}

// Singleton instance shared across the app.
export const realtimeClient = new RealtimeClient();

