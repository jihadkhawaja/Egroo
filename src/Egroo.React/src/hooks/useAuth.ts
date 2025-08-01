import { createContext, useContext } from 'react';
import type { AuthContextType } from '../types';

export const AuthContext = createContext<AuthContextType | undefined>(undefined);

// This hook is now replaced by the one in AuthProvider.tsx
// Kept for backwards compatibility, but should import from AuthProvider instead
export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};