import { useCallback, useEffect, useMemo, useState } from 'react';
import { Box, CircularProgress, CssBaseline, Stack, ThemeProvider, createTheme } from '@mui/material';
import { ApiError, api } from './api';
import { AuthForm } from './AuthForm';
import { Dashboard } from './Dashboard';
import { User } from './types';

type DashboardRoute = 'overview' | 'operations' | 'analytics';
const SESSION_REFRESH_INTERVAL_MS = 10 * 60 * 1000;

const resolveRoute = (pathname: string): DashboardRoute => {
  if (pathname === '/operations' || pathname.startsWith('/operations/')) {
    return 'operations';
  }

  if (pathname === '/analytics' || pathname.startsWith('/analytics/')) {
    return 'analytics';
  }

  return 'overview';
};

const theme = createTheme({
  palette: {
    mode: 'light',
    primary: {
      main: '#0f766e',
      light: '#14b8a6',
    },
    secondary: {
      main: '#d97706',
    },
    divider: 'rgba(148, 163, 184, 0.2)',
    background: {
      default: '#0b1224',
      paper: '#0f1a30',
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
    h4: { fontWeight: 700, letterSpacing: '-0.02em' },
    h5: { fontWeight: 700, letterSpacing: '-0.02em' },
    h6: { fontWeight: 700 },
    subtitle1: { fontWeight: 600 },
    button: { fontWeight: 700, textTransform: 'none' },
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        body: {
          colorScheme: 'dark',
        },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: {
          backgroundImage: 'none',
        },
        outlined: {
          borderColor: 'rgba(148, 163, 184, 0.22)',
        },
      },
    },
    MuiCard: {
      styleOverrides: { root: { backgroundImage: 'none' } },
    },
    MuiButton: {
      defaultProps: {
        disableElevation: true,
      },
      styleOverrides: {
        root: {
          borderRadius: 12,
        },
        containedPrimary: {
          backgroundImage: 'linear-gradient(120deg, #0f766e 0%, #0d9488 100%)',
        },
      },
    },
    MuiTableCell: {
      styleOverrides: {
        head: {
          fontWeight: 700,
          color: '#cbd5e1',
          backgroundColor: 'rgba(15, 23, 42, 0.96)',
        },
      },
    },
    MuiOutlinedInput: {
      styleOverrides: {
        root: {
          backgroundColor: 'rgba(15, 23, 42, 0.35)',
        },
      },
    },
    MuiDialog: {
      styleOverrides: {
        paper: {
          borderRadius: 18,
          border: '1px solid rgba(148, 163, 184, 0.25)',
          backgroundImage: 'none',
        },
      },
    },
    MuiDrawer: {
      styleOverrides: {
        paper: {
          borderRight: '1px solid rgba(148, 163, 184, 0.22)',
        },
      },
    },
  },
});

export function App() {
  const [user, setUser] = useState<User | null>(null);
  const [authLoading, setAuthLoading] = useState(false);
  const [booting, setBooting] = useState(true);
  const [route, setRoute] = useState<DashboardRoute>(() => resolveRoute(window.location.pathname));

  const applyRouteFromLocation = useCallback(() => {
    setRoute(resolveRoute(window.location.pathname));
  }, []);

  useEffect(() => {
    (async () => {
      try {
        const me = await api.me();
        setUser(me);
      } catch {
        setUser(null);
      } finally {
        setBooting(false);
      }
    })();
  }, []);

  useEffect(() => {
    const handler = () => applyRouteFromLocation();
    window.addEventListener('popstate', handler);
    return () => window.removeEventListener('popstate', handler);
  }, [applyRouteFromLocation]);

  useEffect(() => {
    if (!user) {
      return;
    }

    const refreshSession = async () => {
      try {
        const refreshed = await api.refreshSession();
        setUser(refreshed);
      } catch (error) {
        if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
          setUser(null);
        }
      }
    };

    const intervalId = window.setInterval(() => {
      void refreshSession();
    }, SESSION_REFRESH_INTERVAL_MS);

    return () => window.clearInterval(intervalId);
  }, [user]);

  const navigateRoute = useCallback((nextRoute: DashboardRoute) => {
    const nextPath = nextRoute === 'operations' ? '/operations' : nextRoute === 'analytics' ? '/analytics' : '/';
    if (window.location.pathname !== nextPath) {
      window.history.pushState({}, '', nextPath);
    }

    setRoute(nextRoute);
  }, []);

  const handleLogin = async (email: string, password: string, rememberMe: boolean) => {
    setAuthLoading(true);
    try {
      const logged = await api.login(email, password, rememberMe);
      setUser(logged);
    } finally {
      setAuthLoading(false);
    }
  };

  const handleRegister = async (email: string, username: string, password: string) => {
    await api.register(email, username, password);
  };

  const handleLogout = () => {
    setAuthLoading(true);
    api
      .logout()
      .catch(() => {})
      .finally(() => {
        setUser(null);
        setAuthLoading(false);
      });
  };

  const content = useMemo(() => {
    if (booting || authLoading) {
      return (
        <Stack alignItems="center" justifyContent="center" minHeight="100vh">
          <CircularProgress />
        </Stack>
      );
    }

    if (!user) {
      return (
        <Box sx={{ minHeight: '100vh', display: 'grid', placeItems: 'center', px: 2 }}>
          <AuthForm onLogin={handleLogin} onRegister={handleRegister} />
        </Box>
      );
    }

    return <Dashboard onLogout={handleLogout} route={route} onRouteChange={navigateRoute} />;
  }, [authLoading, booting, navigateRoute, route, user]);

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      {content}
    </ThemeProvider>
  );
}

export default App;
