import React from 'react';
import { Typography, Box } from '@mui/material';
import { useParams } from 'react-router-dom';

const ChannelDetailPage: React.FC = () => {
  const { channelId } = useParams<{ channelId: string }>();

  return (
    <Box>
      <Typography variant="h4" component="h1" gutterBottom>
        Channel Details
      </Typography>
      <Typography variant="body1">
        Channel ID: {channelId} - to be implemented
      </Typography>
    </Box>
  );
};

export default ChannelDetailPage;