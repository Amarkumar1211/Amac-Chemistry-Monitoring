import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { authApi, tokenStore } from '../services/api';
import { realtimeClient } from '../services/signalr';
import type { CurrentUser, LoginRequest } from '../types';

interface AuthContextValue {
  user: CurrentUser | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (credentials: LoginRequest) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(() => tokenStore.getUser());
  const [isLoading, setIsLoading] = useState(true);

  const login = useCallback(async (credentials: LoginRequest) => {
    const auth = await authApi.login({
      userName: credentials.userName,
      password: credentials.password,
    });
    tokenStore.setSession(auth);
    setUser(tokenStore.getUser());
    await realtimeClient.connect();
  }, []);

  const logout = useCallback(async () => {
    try {
      await authApi.logout();
    } catch {
      // ignore logout network error
    }
    await realtimeClient.disconnect();
    tokenStore.clear();
    setUser(null);
  }, []);

  useEffect(() => {
    const restore = async () => {
      if (tokenStore.getToken()) {
        try {
          const me = await authApi.me();
          setUser(me);
          await realtimeClient.connect();
        } catch {
          tokenStore.clear();
          setUser(null);
        }
      }
      setIsLoading(false);
    };
    void restore();
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: !!user,
      isLoading,
      login,
      logout,
    }),
    [user, isLoading, login, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
