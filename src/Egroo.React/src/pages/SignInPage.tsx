import React, { useState, useEffect } from 'react';
import {
  Box,
  Button,
  Card,
  CardActions,
  CardContent,
  CardHeader,
  Container,
  TextField,
  Typography,
  IconButton,
  Alert,
  CircularProgress,
} from '@mui/material';
import {
  Home as HomeIcon,
} from '@mui/icons-material';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../services/AuthProvider';

const SignInPage: React.FC = () => {
  const navigate = useNavigate();
  const { signIn, isAuthenticated } = useAuth();
  
  const [formData, setFormData] = useState({
    username: '',
    password: '',
  });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [isLoading, setIsLoading] = useState(false);
  const [generalError, setGeneralError] = useState<string | null>(null);

  // Redirect authenticated users to channels
  useEffect(() => {
    if (isAuthenticated) {
      navigate('/channels', { replace: true });
    }
  }, [isAuthenticated, navigate]);

  const handleInputChange = (field: string) => (event: React.ChangeEvent<HTMLInputElement>) => {
    setFormData(prev => ({
      ...prev,
      [field]: event.target.value,
    }));
    
    // Clear error when user starts typing
    if (errors[field]) {
      setErrors(prev => ({
        ...prev,
        [field]: '',
      }));
    }
  };

  const validateForm = (): boolean => {
    const newErrors: Record<string, string> = {};
    
    if (!formData.username.trim()) {
      newErrors.username = 'Username is required';
    }
    
    if (!formData.password.trim()) {
      newErrors.password = 'Password is required';
    }
    
    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setGeneralError(null);
    
    if (!validateForm()) {
      return;
    }
    
    setIsLoading(true);
    
    try {
      await signIn({
        username: formData.username,
        password: formData.password,
      });
      
      // Navigate to channels on successful sign in
      navigate('/channels');
    } catch (error: any) {
      console.error('Sign in error:', error);
      setGeneralError(
        error.response?.data?.message || 
        error.message || 
        'Failed to sign in. Please try again.'
      );
    } finally {
      setIsLoading(false);
    }
  };

  const handleHomeClick = () => {
    navigate('/');
  };

  const handleSignUpClick = () => {
    navigate('/signup');
  };

  return (
    <Container maxWidth="lg" sx={{ my: 10, pt: 8 }}>
      <IconButton 
        onClick={handleHomeClick}
        size="large" 
        sx={{ mb: 4 }}
      >
        <HomeIcon />
      </IconButton>
      
      <Typography 
        variant="h6" 
        color="secondary" 
        gutterBottom
      >
        Sign In
      </Typography>
      
      <Typography variant="body1">
        Don't have an account?{' '}
        <Button 
          onClick={handleSignUpClick}
          variant="text"
          sx={{ textTransform: 'uppercase' }}
        >
          SIGNUP
        </Button>
      </Typography>

      <Box 
        sx={{ 
          mt: 8, 
          display: 'flex', 
          justifyContent: 'center' 
        }}
      >
        <Box sx={{ width: { xs: '100%', sm: '66%', md: '50%' } }}>
          <Card 
            elevation={8} 
            sx={{ 
              borderRadius: 2, 
              pb: 2 
            }}
          >
            <CardHeader
              title={
                <Typography variant="h5" align="left">
                  Login Account
                </Typography>
              }
            />
            <CardContent>
              {generalError && (
                <Alert severity="error" sx={{ mb: 2 }}>
                  {generalError}
                </Alert>
              )}
              
              <Box component="form" onSubmit={handleSubmit}>
                <TextField
                  fullWidth
                  label="Username"
                  value={formData.username}
                  onChange={handleInputChange('username')}
                  error={!!errors.username}
                  helperText={errors.username}
                  margin="normal"
                  required
                  disabled={isLoading}
                />
                
                <TextField
                  fullWidth
                  label="Password"
                  type="password"
                  value={formData.password}
                  onChange={handleInputChange('password')}
                  error={!!errors.password}
                  helperText={errors.password}
                  margin="normal"
                  required
                  disabled={isLoading}
                />
              </Box>
            </CardContent>
            
            <CardActions sx={{ display: 'flex', justifyContent: 'center' }}>
              <Button
                onClick={handleSubmit}
                variant="contained"
                color="primary"
                size="large"
                disabled={isLoading}
                sx={{ width: '50%', textTransform: 'uppercase' }}
                startIcon={isLoading ? <CircularProgress size={20} /> : undefined}
              >
                {isLoading ? 'Signing In...' : 'Submit'}
              </Button>
            </CardActions>
          </Card>
        </Box>
      </Box>
    </Container>
  );
};

export default SignInPage;