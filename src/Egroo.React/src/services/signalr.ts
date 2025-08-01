import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { API_CONFIG, STORAGE_KEYS } from '../utils/config';
import type { Message, User } from '../types';

export interface ChatHubEvents {
  // Message events
  onMessageReceived: (message: Message) => void;
  onMessageDeleted: (messageId: string) => void;
  
  // User status events
  onUserOnline: (user: User) => void;
  onUserOffline: (user: User) => void;
  onFriendStatusChanged: (userId: string, isOnline: boolean) => void;
  
  // Channel events
  onChannelJoined: (channelId: string, user: User) => void;
  onChannelLeft: (channelId: string, user: User) => void;
  
  // Call events (for future WebRTC integration)
  onCallOffer: (offer: any) => void;
  onCallAnswer: (answer: any) => void;
  onCallEnd: () => void;
}

class SignalRService {
  private connection: HubConnection | null = null;
  private isConnected = false;
  private eventHandlers: Partial<ChatHubEvents> = {};

  async connect(): Promise<void> {
    if (this.connection && this.isConnected) {
      return;
    }

    const token = localStorage.getItem(STORAGE_KEYS.AUTH_TOKEN);
    if (!token) {
      throw new Error('No authentication token found');
    }

    this.connection = new HubConnectionBuilder()
      .withUrl(API_CONFIG.HUB_URL, {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    // Register event handlers
    this.registerEventHandlers();

    try {
      await this.connection.start();
      this.isConnected = true;
      console.log('SignalR Connected');
    } catch (error) {
      console.error('SignalR Connection failed:', error);
      throw error;
    }
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
      this.isConnected = false;
      console.log('SignalR Disconnected');
    }
  }

  private registerEventHandlers(): void {
    if (!this.connection) return;

    // Message events
    this.connection.on('MessageReceived', (message: Message) => {
      this.eventHandlers.onMessageReceived?.(message);
    });

    this.connection.on('MessageDeleted', (messageId: string) => {
      this.eventHandlers.onMessageDeleted?.(messageId);
    });

    // User status events
    this.connection.on('UserOnline', (user: User) => {
      this.eventHandlers.onUserOnline?.(user);
    });

    this.connection.on('UserOffline', (user: User) => {
      this.eventHandlers.onUserOffline?.(user);
    });

    this.connection.on('FriendStatusChanged', (userId: string, isOnline: boolean) => {
      this.eventHandlers.onFriendStatusChanged?.(userId, isOnline);
    });

    // Channel events
    this.connection.on('ChannelJoined', (channelId: string, user: User) => {
      this.eventHandlers.onChannelJoined?.(channelId, user);
    });

    this.connection.on('ChannelLeft', (channelId: string, user: User) => {
      this.eventHandlers.onChannelLeft?.(channelId, user);
    });

    // Call events
    this.connection.on('CallOffer', (offer: any) => {
      this.eventHandlers.onCallOffer?.(offer);
    });

    this.connection.on('CallAnswer', (answer: any) => {
      this.eventHandlers.onCallAnswer?.(answer);
    });

    this.connection.on('CallEnd', () => {
      this.eventHandlers.onCallEnd?.();
    });

    // Connection state events
    this.connection.onreconnecting(() => {
      this.isConnected = false;
      console.log('SignalR Reconnecting...');
    });

    this.connection.onreconnected(() => {
      this.isConnected = true;
      console.log('SignalR Reconnected');
    });

    this.connection.onclose(() => {
      this.isConnected = false;
      console.log('SignalR Connection Closed');
    });
  }

  // Event subscription methods
  on<K extends keyof ChatHubEvents>(event: K, handler: ChatHubEvents[K]): void {
    this.eventHandlers[event] = handler;
  }

  off<K extends keyof ChatHubEvents>(event: K): void {
    delete this.eventHandlers[event];
  }

  // Hub method calls
  async sendMessage(channelId: string, content: string): Promise<void> {
    if (!this.connection || !this.isConnected) {
      throw new Error('SignalR connection is not established');
    }

    await this.connection.invoke('SendMessage', channelId, content);
  }

  async joinChannel(channelId: string): Promise<void> {
    if (!this.connection || !this.isConnected) {
      throw new Error('SignalR connection is not established');
    }

    await this.connection.invoke('JoinChannel', channelId);
  }

  async leaveChannel(channelId: string): Promise<void> {
    if (!this.connection || !this.isConnected) {
      throw new Error('SignalR connection is not established');
    }

    await this.connection.invoke('LeaveChannel', channelId);
  }

  async deleteMessage(messageId: string): Promise<void> {
    if (!this.connection || !this.isConnected) {
      throw new Error('SignalR connection is not established');
    }

    await this.connection.invoke('DeleteMessage', messageId);
  }

  // Call methods (for future WebRTC implementation)
  async makeCall(userId: string, sdpOffer: string): Promise<void> {
    if (!this.connection || !this.isConnected) {
      throw new Error('SignalR connection is not established');
    }

    await this.connection.invoke('MakeCall', userId, sdpOffer);
  }

  async answerCall(userId: string, sdpAnswer: string): Promise<void> {
    if (!this.connection || !this.isConnected) {
      throw new Error('SignalR connection is not established');
    }

    await this.connection.invoke('AnswerCall', userId, sdpAnswer);
  }

  async endCall(userId: string): Promise<void> {
    if (!this.connection || !this.isConnected) {
      throw new Error('SignalR connection is not established');
    }

    await this.connection.invoke('EndCall', userId);
  }

  // Getters
  get connected(): boolean {
    return this.isConnected;
  }

  get connectionState(): string {
    return this.connection?.state || 'Disconnected';
  }
}

export const signalRService = new SignalRService();
export default signalRService;