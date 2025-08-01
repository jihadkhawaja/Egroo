import React, { useState } from 'react';
import {
  Box,
  Button,
  Card,
  CardContent,
  Typography,
  Alert,
  CircularProgress,
} from '@mui/material';
import { API_CONFIG } from '../utils/config';

const AboutPage: React.FC = () => {
  const [apiStatus, setApiStatus] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const testApiConnection = async () => {
    setLoading(true);
    setError(null);
    setApiStatus(null);

    try {
      const response = await fetch(`${API_CONFIG.BASE_URL}/api/test/health`);
      const data = await response.json();
      setApiStatus(`✅ API Connection Successful: ${data.message}`);
    } catch (err: any) {
      setError(`❌ API Connection Failed: ${err.message}`);
    } finally {
      setLoading(false);
    }
  };

  const testCors = async () => {
    setLoading(true);
    setError(null);
    setApiStatus(null);

    try {
      const response = await fetch(`${API_CONFIG.BASE_URL}/api/test/cors`);
      const data = await response.json();
      setApiStatus(`✅ CORS Working: ${data.message}`);
    } catch (err: any) {
      setError(`❌ CORS Failed: ${err.message}`);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box>
      <Typography variant="h4" component="h1" gutterBottom>
        About Egroo
      </Typography>
      
      <Typography variant="body1" paragraph>
        Egroo is a self-hosted chatting solution with a cross-platform client application.
        This React TypeScript frontend communicates with the .NET Web API backend.
      </Typography>

      <Card sx={{ mt: 4 }}>
        <CardContent>
          <Typography variant="h6" gutterBottom>
            Backend Integration Test
          </Typography>
          
          <Typography variant="body2" paragraph>
            API Base URL: {API_CONFIG.BASE_URL}
          </Typography>
          
          <Box sx={{ mb: 2 }}>
            <Button
              variant="contained"
              onClick={testApiConnection}
              disabled={loading}
              sx={{ mr: 2 }}
            >
              {loading ? <CircularProgress size={20} /> : 'Test API Health'}
            </Button>
            
            <Button
              variant="contained"
              color="secondary"
              onClick={testCors}
              disabled={loading}
            >
              {loading ? <CircularProgress size={20} /> : 'Test CORS'}
            </Button>
          </Box>

          {apiStatus && (
            <Alert severity="success" sx={{ mt: 2 }}>
              {apiStatus}
            </Alert>
          )}
          
          {error && (
            <Alert severity="error" sx={{ mt: 2 }}>
              {error}
            </Alert>
          )}
        </CardContent>
      </Card>

      <Card sx={{ mt: 3 }}>
        <CardContent>
          <Typography variant="h6" gutterBottom>
            Migration Status
          </Typography>
          
          <Typography variant="body2" component="div">
            <strong>✅ Completed:</strong>
            <ul>
              <li>React TypeScript setup with Vite</li>
              <li>Material UI 3 theme configuration</li>
              <li>API service layer with axios</li>
              <li>SignalR integration for real-time features</li>
              <li>Authentication context and JWT handling</li>
              <li>Homepage and Sign In page migration</li>
              <li>CORS configuration for React integration</li>
            </ul>
          </Typography>
          
          <Typography variant="body2" component="div" sx={{ mt: 2 }}>
            <strong>🚧 In Progress:</strong>
            <ul>
              <li>Backend API integration testing</li>
              <li>Channels and messaging interface</li>
              <li>User management features</li>
            </ul>
          </Typography>
        </CardContent>
      </Card>
    </Box>
  );
};

export default AboutPage;