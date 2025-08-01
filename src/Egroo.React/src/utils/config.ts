// API Configuration
export const API_CONFIG = {
  // Development URL matching the .NET backend
  BASE_URL: process.env.NODE_ENV === 'production' 
    ? 'https://api.egroo.org' 
    : 'http://localhost:5175',
  
  // API endpoints
  ENDPOINTS: {
    AUTH: {
      SIGN_IN: '/api/v1/Auth/signin',
      SIGN_UP: '/api/v1/Auth/signup',
      REFRESH_SESSION: '/api/v1/Auth/refreshsession',
      CHANGE_PASSWORD: '/api/v1/Auth/changepassword',
    },
    // These will be added as we discover more endpoints
    CHANNELS: '/api/v1/channels',
    USERS: '/api/v1/users',
    MESSAGES: '/api/v1/messages',
  },
  
  // SignalR Hub
  HUB_URL: process.env.NODE_ENV === 'production' 
    ? 'https://api.egroo.org/chathub' 
    : 'http://localhost:5175/chathub',
};

// Storage keys
export const STORAGE_KEYS = {
  AUTH_TOKEN: 'egroo_auth_token',
  USER_DATA: 'egroo_user_data',
} as const;