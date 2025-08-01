import React, { createContext, useContext, useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { apiService } from '../services/api';
import { signalRService } from '../services/signalr';
import { STORAGE_KEYS } from '../utils/config';
import type { AuthContextType, User, SignInRequest, SignUpRequest } from '../types';

interface AuthProviderProps {
  children: ReactNode;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<AuthProviderProps> = ({ children }) => {
  const [user, setUser] = useState<User | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  // Initialize auth state from localStorage
  useEffect(() => {
    const initializeAuth = async () => {
      try {
        const storedToken = localStorage.getItem(STORAGE_KEYS.AUTH_TOKEN);
        const storedUser = localStorage.getItem(STORAGE_KEYS.USER_DATA);

        if (storedToken && storedUser) {
          setToken(storedToken);
          setUser(JSON.parse(storedUser));
          
          // Try to connect to SignalR
          try {
            await signalRService.connect();
          } catch (error) {
            console.error('Failed to connect to SignalR:', error);
            // Don't sign out the user just because SignalR failed
          }

          // Optionally refresh the session to validate the token
          try {
            const refreshedAuth = await apiService.refreshSession();
            setUser(refreshedAuth.user);
            setToken(refreshedAuth.token);
            localStorage.setItem(STORAGE_KEYS.AUTH_TOKEN, refreshedAuth.token);
            localStorage.setItem(STORAGE_KEYS.USER_DATA, JSON.stringify(refreshedAuth.user));
          } catch (error) {
            console.warn('Session refresh failed, using cached data');
          }
        }
      } catch (error) {
        console.error('Auth initialization error:', error);
        // Clear invalid data
        localStorage.removeItem(STORAGE_KEYS.AUTH_TOKEN);
        localStorage.removeItem(STORAGE_KEYS.USER_DATA);
      } finally {
        setIsLoading(false);
      }
    };

    initializeAuth();
  }, []);

  const signIn = async (credentials: SignInRequest): Promise<void> => {
    try {
      const authResponse = await apiService.signIn(credentials);
      
      setUser(authResponse.user);
      setToken(authResponse.token);
      
      // Store in localStorage
      localStorage.setItem(STORAGE_KEYS.AUTH_TOKEN, authResponse.token);
      localStorage.setItem(STORAGE_KEYS.USER_DATA, JSON.stringify(authResponse.user));
      
      // Connect to SignalR
      try {
        await signalRService.connect();
      } catch (error) {
        console.error('Failed to connect to SignalR after sign in:', error);
      }
    } catch (error) {
      console.error('Sign in error:', error);
      throw error;
    }
  };

  const signUp = async (credentials: SignUpRequest): Promise<void> => {
    try {
      const authResponse = await apiService.signUp(credentials);
      
      setUser(authResponse.user);
      setToken(authResponse.token);
      
      // Store in localStorage
      localStorage.setItem(STORAGE_KEYS.AUTH_TOKEN, authResponse.token);
      localStorage.setItem(STORAGE_KEYS.USER_DATA, JSON.stringify(authResponse.user));
      
      // Connect to SignalR
      try {
        await signalRService.connect();
      } catch (error) {
        console.error('Failed to connect to SignalR after sign up:', error);
      }
    } catch (error) {
      console.error('Sign up error:', error);
      throw error;
    }
  };

  const signOut = async (): Promise<void> => {
    try {
      // Disconnect from SignalR
      await signalRService.disconnect();
    } catch (error) {
      console.error('Error disconnecting from SignalR:', error);
    }
    
    // Clear state
    setUser(null);
    setToken(null);
    
    // Clear localStorage
    localStorage.removeItem(STORAGE_KEYS.AUTH_TOKEN);
    localStorage.removeItem(STORAGE_KEYS.USER_DATA);
  };

  const contextValue: AuthContextType = {
    user,
    token,
    isAuthenticated: !!user && !!token,
    signIn,
    signUp,
    signOut,
  };

  // Show loading screen while initializing
  if (isLoading) {
    return (
      <div style={{ 
        display: 'flex', 
        justifyContent: 'center', 
        alignItems: 'center', 
        height: '100vh' 
      }}>
        Loading...
      </div>
    );
  }

  return (
    <AuthContext.Provider value={contextValue}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = (): AuthContextType => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};

export default AuthProvider;