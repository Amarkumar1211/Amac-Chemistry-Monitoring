import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { realtimeClient } from '../services/signalr';
import type { FurnaceClearedDto, FurnaceUpdateDto } from '../types';

interface SignalRContextValue {
  connected: boolean;
  subscribeUpdate: (handler: (update: FurnaceUpdateDto) => void) => () => void;
  subscribeClear: (handler: (cleared: FurnaceClearedDto) => void) => () => void;
  connect: () => Promise<void>;
  disconnect: () => Promise<void>;
  setSystemLive: (isLive: boolean) => Promise<void>;
}

const SignalRContext = createContext<SignalRContextValue | undefined>(undefined);

export function SignalRProvider({ children }: { children: ReactNode }) {
  const [connected, setConnected] = useState(() => realtimeClient.isConnected());

  useEffect(() => {
    const unsub = realtimeClient.onState(setConnected);
    return unsub;
  }, []);

  const subscribeUpdate = useCallback((handler: (u: FurnaceUpdateDto) => void) => {
    return realtimeClient.onUpdate(handler);
  }, []);

  const subscribeClear = useCallback((handler: (c: FurnaceClearedDto) => void) => {
    return realtimeClient.onClear(handler);
  }, []);

  const connect = useCallback(() => realtimeClient.connect(), []);
  const disconnect = useCallback(() => realtimeClient.disconnect(), []);
  const setSystemLive = useCallback((isLive: boolean) => realtimeClient.setSystemLive(isLive), []);

  const value = useMemo<SignalRContextValue>(
    () => ({ connected, subscribeUpdate, subscribeClear, connect, disconnect, setSystemLive }),
    [connected, subscribeUpdate, subscribeClear, connect, disconnect, setSystemLive],
  );

  return <SignalRContext.Provider value={value}>{children}</SignalRContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function useSignalR(): SignalRContextValue {
  const ctx = useContext(SignalRContext);
  if (!ctx) throw new Error('useSignalR must be used within SignalRProvider');
  return ctx;
}
