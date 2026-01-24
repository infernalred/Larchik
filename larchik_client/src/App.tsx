import { useMemo, useState } from 'react';
import { Box, CircularProgress, CssBaseline, Stack, ThemeProvider, createTheme } from '@mui/material';
import { api } from './api';
import { AuthForm } from './AuthForm';
import { Dashboard } from './Dashboard';
import { User } from './types';

const theme = createTheme({
  palette: {
    mode: 'light',
    primary: {
      main: '#0f766e',
    },
    secondary: {
      main: '#d97706',
    },
    background: {
      default: '#0b1224',
      paper: '#0f172a',
    },
    text: {
      primary: '#e2e8f0',
      secondary: '#94a3b8',
    },
  },
  shape: {
    borderRadius: 16,
  },
  typography: {
    fontFamily: '"Space Grotesk","Inter","IBM Plex Sans",system-ui,-apple-system,sans-serif',
    h6: { fontWeight: 700 },
    subtitle1: { fontWeight: 600 },
  },
  components: {
    MuiPaper: {
      styleOverrides: { root: { backgroundImage: 'none' } },
    },
    MuiCard: {
      styleOverrides: { root: { backgroundImage: 'none' } },
    },
  },
});

export function App() {
  const [user, setUser] = useState<User | null>(() => {
    const saved = localStorage.getItem('user');
    return saved ? (JSON.parse(saved) as User) : null;
  });
  const [authLoading, setAuthLoading] = useState(false);

  const handleLogin = async (email: string, password: string) => {
    setAuthLoading(true);
    const logged = await api.login(email, password);
    localStorage.setItem('token', logged.token);
    localStorage.setItem('user', JSON.stringify(logged));
    setUser(logged);
    setAuthLoading(false);
  };

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    setUser(null);
  };

  const content = useMemo(() => {
    if (authLoading) {
      return (
        <Stack alignItems="center" justifyContent="center" minHeight="100vh">
          <CircularProgress />
        </Stack>
      );
    }

    if (!user) {
      return (
        <Box sx={{ minHeight: '100vh', display: 'grid', placeItems: 'center', px: 2 }}>
          <AuthForm initialEmail="admin@test.com" initialPassword="Password!!!123" onSubmit={handleLogin} />
        </Box>
      );
    }

    return <Dashboard onLogout={handleLogout} />;
  }, [authLoading, user]);

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      {content}
    </ThemeProvider>
  );
}

export default App;
