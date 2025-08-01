// API Types based on the .NET backend models
export interface User {
  id: string;
  username: string;
  email: string;
  avatar?: MediaResult;
}

export interface MediaResult {
  data: string;
  contentType: string;
}

export interface Channel {
  id: string;
  name: string;
  description?: string;
  isPrivate: boolean;
  createdAt: Date;
  members: ChannelMember[];
}

export interface ChannelMember {
  id: string;
  userId: string;
  channelId: string;
  user: User;
  joinedAt: Date;
}

export interface Message {
  id: string;
  content: string;
  channelId: string;
  userId: string;
  user: User;
  createdAt: Date;
  messageType: MessageType;
}

export const MessageType = {
  Text: 0,
  Image: 1,
  File: 2,
} as const;

export type MessageType = typeof MessageType[keyof typeof MessageType];

export interface AuthResponse {
  token: string;
  user: User;
}

export interface SignInRequest {
  username: string;
  password: string;
}

export interface SignUpRequest {
  username: string;
  email: string;
  password: string;
}

// Frontend specific types
export interface AuthContextType {
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
  signIn: (credentials: SignInRequest) => Promise<void>;
  signUp: (credentials: SignUpRequest) => Promise<void>;
  signOut: () => void;
}

export interface SessionData {
  user: User | null;
  token: string | null;
}