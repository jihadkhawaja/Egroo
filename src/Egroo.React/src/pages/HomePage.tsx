import React from 'react';
import {
  AppBar,
  Box,
  Button,
  Card,
  CardContent,
  Container,
  IconButton,
  Stack,
  Toolbar,
  Typography,
} from '@mui/material';
import {
  GitHub as GitHubIcon,
  Login as LoginIcon,
} from '@mui/icons-material';
import { useNavigate } from 'react-router-dom';

const HomePage: React.FC = () => {
  const navigate = useNavigate();

  const handleSignInClick = () => {
    navigate('/signin');
  };

  return (
    <Box sx={{ flexGrow: 1 }}>
      <AppBar position="static" elevation={2}>
        <Toolbar>
          <Box sx={{ display: 'flex', alignItems: 'center', mr: 2 }}>
            <img 
              src="/favicon-32x32.png" 
              alt="Egroo.org" 
              width="32" 
              height="32" 
              style={{ marginRight: 16 }}
            />
            <Typography variant="h6" component="div">
              Egroo
            </Typography>
          </Box>
          <Box sx={{ flexGrow: 1 }} />
          <IconButton
            color="inherit"
            href="https://github.com/jihadkhawaja/Egroo"
            target="_blank"
            rel="noopener noreferrer"
          >
            <GitHubIcon />
          </IconButton>
          <IconButton
            color="inherit"
            onClick={handleSignInClick}
          >
            <LoginIcon />
          </IconButton>
        </Toolbar>
      </AppBar>

      <Container maxWidth="lg" sx={{ my: 4, pt: 4 }}>
        <Typography variant="h3" component="h1" gutterBottom>
          Welcome to Egroo!
        </Typography>
        <Typography variant="body1" sx={{ mt: 4 }}>
          Egroo is a self-hosted chatting solution with a cross-platform client application.
          Enjoy secure, private messaging while maintaining control over your data.
        </Typography>

        <Box 
          sx={{ 
            mt: 8,
            display: 'grid',
            gridTemplateColumns: { xs: '1fr', md: 'repeat(3, 1fr)' },
            gap: 3
          }}
        >
          <Card>
            <CardContent>
              <Typography variant="h5" component="h2" gutterBottom>
                🔒 Secure Messaging
              </Typography>
              <Typography variant="body2">
                Keep your conversations private with end-to-end encryption.
              </Typography>
            </CardContent>
          </Card>

          <Card>
            <CardContent>
              <Typography variant="h5" component="h2" gutterBottom>
                🧑‍🤝‍🧑 Cross-Platform
              </Typography>
              <Typography variant="body2">
                Access your chats on any device – desktop, mobile, or web.
              </Typography>
            </CardContent>
          </Card>

          <Card>
            <CardContent>
              <Typography variant="h5" component="h2" gutterBottom>
                🚀 Open Source
              </Typography>
              <Typography variant="body2">
                Completely open-source – customize and extend as you like.
              </Typography>
            </CardContent>
          </Card>
        </Box>

        <Stack direction="row" spacing={3} sx={{ mt: 12 }}>
          <Button
            variant="contained"
            color="primary"
            startIcon={<GitHubIcon />}
            href="https://github.com/jihadkhawaja/Egroo"
            target="_blank"
            rel="noopener noreferrer"
          >
            GitHub Repository
          </Button>
          <Button
            variant="contained"
            color="secondary"
            startIcon={<LoginIcon />}
            onClick={handleSignInClick}
          >
            Sign In
          </Button>
        </Stack>
      </Container>
    </Box>
  );
};

export default HomePage;