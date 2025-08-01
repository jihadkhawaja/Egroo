import axios from 'axios';
import type { AxiosInstance, AxiosResponse } from 'axios';
import { API_CONFIG, STORAGE_KEYS } from '../utils/config';
import type { 
  User, 
  AuthResponse, 
  SignInRequest, 
  SignUpRequest,
  Channel,
  Message
} from '../types';

class ApiService {
  private api: AxiosInstance;

  constructor() {
    this.api = axios.create({
      baseURL: API_CONFIG.BASE_URL,
      timeout: 10000,
      headers: {
        'Content-Type': 'application/json',
      },
    });

    // Request interceptor to add auth token
    this.api.interceptors.request.use(
      (config) => {
        const token = localStorage.getItem(STORAGE_KEYS.AUTH_TOKEN);
        if (token) {
          config.headers.Authorization = `Bearer ${token}`;
        }
        return config;
      },
      (error) => Promise.reject(error)
    );

    // Response interceptor to handle auth errors
    this.api.interceptors.response.use(
      (response) => response,
      (error) => {
        if (error.response?.status === 401) {
          // Token expired or invalid
          localStorage.removeItem(STORAGE_KEYS.AUTH_TOKEN);
          localStorage.removeItem(STORAGE_KEYS.USER_DATA);
          window.location.href = '/signin';
        }
        return Promise.reject(error);
      }
    );
  }

  // Authentication
  async signIn(credentials: SignInRequest): Promise<AuthResponse> {
    const response: AxiosResponse<AuthResponse> = await this.api.post(
      API_CONFIG.ENDPOINTS.AUTH.SIGN_IN,
      credentials
    );
    return response.data;
  }

  async signUp(credentials: SignUpRequest): Promise<AuthResponse> {
    const response: AxiosResponse<AuthResponse> = await this.api.post(
      API_CONFIG.ENDPOINTS.AUTH.SIGN_UP,
      credentials
    );
    return response.data;
  }

  async refreshSession(): Promise<AuthResponse> {
    const response: AxiosResponse<AuthResponse> = await this.api.get(
      API_CONFIG.ENDPOINTS.AUTH.REFRESH_SESSION
    );
    return response.data;
  }

  async changePassword(oldPassword: string, newPassword: string): Promise<void> {
    await this.api.put(API_CONFIG.ENDPOINTS.AUTH.CHANGE_PASSWORD, {
      oldPassword,
      newPassword,
    });
  }

  // User Management
  async getCurrentUser(): Promise<User> {
    const response: AxiosResponse<User> = await this.api.get('/api/v1/user/current');
    return response.data;
  }

  async getUserAvatar(userId: string): Promise<Blob> {
    const response = await this.api.get(`/api/v1/user/${userId}/avatar`, {
      responseType: 'blob',
    });
    return response.data;
  }

  async updateUserAvatar(file: File): Promise<void> {
    const formData = new FormData();
    formData.append('avatar', file);
    await this.api.put('/api/v1/user/avatar', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
  }

  // Channels
  async getChannels(): Promise<Channel[]> {
    const response: AxiosResponse<Channel[]> = await this.api.get(API_CONFIG.ENDPOINTS.CHANNELS);
    return response.data;
  }

  async getChannel(channelId: string): Promise<Channel> {
    const response: AxiosResponse<Channel> = await this.api.get(
      `${API_CONFIG.ENDPOINTS.CHANNELS}/${channelId}`
    );
    return response.data;
  }

  async createChannel(name: string, description?: string, isPrivate = false): Promise<Channel> {
    const response: AxiosResponse<Channel> = await this.api.post(API_CONFIG.ENDPOINTS.CHANNELS, {
      name,
      description,
      isPrivate,
    });
    return response.data;
  }

  async joinChannel(channelId: string): Promise<void> {
    await this.api.post(`${API_CONFIG.ENDPOINTS.CHANNELS}/${channelId}/join`);
  }

  async leaveChannel(channelId: string): Promise<void> {
    await this.api.post(`${API_CONFIG.ENDPOINTS.CHANNELS}/${channelId}/leave`);
  }

  // Messages
  async getMessages(channelId: string, page = 1, limit = 50): Promise<Message[]> {
    const response: AxiosResponse<Message[]> = await this.api.get(
      `${API_CONFIG.ENDPOINTS.MESSAGES}?channelId=${channelId}&page=${page}&limit=${limit}`
    );
    return response.data;
  }

  async sendMessage(channelId: string, content: string): Promise<Message> {
    const response: AxiosResponse<Message> = await this.api.post(API_CONFIG.ENDPOINTS.MESSAGES, {
      channelId,
      content,
      messageType: 0, // Text message
    });
    return response.data;
  }

  async deleteMessage(messageId: string): Promise<void> {
    await this.api.delete(`${API_CONFIG.ENDPOINTS.MESSAGES}/${messageId}`);
  }

  // Friends
  async getFriends(): Promise<User[]> {
    const response: AxiosResponse<User[]> = await this.api.get('/api/v1/friends');
    return response.data;
  }

  async sendFriendRequest(username: string): Promise<void> {
    await this.api.post('/api/v1/friends/request', { username });
  }

  async acceptFriendRequest(userId: string): Promise<void> {
    await this.api.post(`/api/v1/friends/accept/${userId}`);
  }

  async rejectFriendRequest(userId: string): Promise<void> {
    await this.api.post(`/api/v1/friends/reject/${userId}`);
  }

  async removeFriend(userId: string): Promise<void> {
    await this.api.delete(`/api/v1/friends/${userId}`);
  }
}

export const apiService = new ApiService();
export default apiService;