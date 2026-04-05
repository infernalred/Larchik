import { useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Checkbox,
  FormControlLabel,
  Stack,
  TextField,
  Typography,
} from '@mui/material';

type AuthMode = 'login' | 'register';

interface Props {
  onLogin: (email: string, password: string, rememberMe: boolean) => Promise<void>;
  onRegister: (email: string, username: string, password: string) => Promise<void>;
}

function getAuthErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof Error)) return fallback;

  try {
    const payload = JSON.parse(error.message) as
      | {
          title?: string;
          errors?: Record<string, string[]>;
          message?: string;
        }
      | Array<{ description?: string }>;

    if (Array.isArray(payload)) {
      const identityErrors = payload.map((item) => item.description).filter(Boolean);
      if (identityErrors.length > 0) {
        return identityErrors[0]!;
      }
    } else {
      const validationErrors = payload.errors
        ? Object.values(payload.errors)
            .flat()
            .filter(Boolean)
        : [];

      if (validationErrors.length > 0) {
        return validationErrors[0];
      }

      return payload.message || payload.title || error.message || fallback;
    }
  } catch {
    return error.message || fallback;
  }

  return error.message || fallback;
}

export function AuthForm({ onLogin, onRegister }: Props) {
  const [mode, setMode] = useState<AuthMode>('login');
  const [email, setEmail] = useState('');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [rememberMe, setRememberMe] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [loading, setLoading] = useState(false);

  const isRegister = mode === 'register';

  const handleModeChange = (nextMode: AuthMode) => {
    if (loading || mode === nextMode) {
      return;
    }

    setMode(nextMode);
    setError('');
    setSuccess('');
    setPassword('');
    setConfirmPassword('');
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setSuccess('');

    if (isRegister && password !== confirmPassword) {
      setError('Пароли не совпадают.');
      return;
    }

    setLoading(true);

    try {
      if (isRegister) {
        await onRegister(email, username, password);
        setSuccess('Аккаунт создан. Подтвердите email и затем войдите.');
        setMode('login');
        setPassword('');
        setConfirmPassword('');
        setRememberMe(true);
      } else {
        await onLogin(email, password, rememberMe);
      }
    } catch (err) {
      setError(
        getAuthErrorMessage(
          err,
          isRegister
            ? 'Не удалось зарегистрироваться. Проверьте введенные данные.'
            : 'Не удалось войти. Проверьте email/пароль.',
        ),
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <Card
      sx={{
        width: '100%',
        maxWidth: 460,
        mx: 'auto',
        p: { xs: 0.25, sm: 1 },
        border: '1px solid rgba(148, 163, 184, 0.24)',
        background: 'linear-gradient(160deg, rgba(15,118,110,0.12) 0%, rgba(15,23,42,0.92) 45%)',
      }}
    >
      <CardContent sx={{ p: { xs: 2, sm: 2.5 } }}>
        <Stack spacing={1.5}>
          <Box>
            <Typography variant="h5" fontWeight={700} gutterBottom>
              Larchik Investments
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Управляйте портфелями, следите за P&L и потоками в одной панели.
            </Typography>
          </Box>

          <Stack direction="row" spacing={1}>
            <Button
              variant={isRegister ? 'outlined' : 'contained'}
              onClick={() => handleModeChange('login')}
              disabled={loading}
              fullWidth
            >
              Вход
            </Button>
            <Button
              variant={isRegister ? 'contained' : 'outlined'}
              onClick={() => handleModeChange('register')}
              disabled={loading}
              fullWidth
            >
              Регистрация
            </Button>
          </Stack>
        </Stack>

        <Box component="form" onSubmit={handleSubmit} sx={{ mt: 2 }}>
          <Stack spacing={2}>
            {isRegister && (
              <TextField
                label="Имя пользователя"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                autoFocus
                fullWidth
              />
            )}
            <TextField
              label="Email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              autoFocus={!isRegister}
              fullWidth
            />
            <TextField
              label="Пароль"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              fullWidth
              helperText={isRegister ? 'Минимум 8 символов, 1 заглавная, 1 строчная и 1 цифра.' : undefined}
            />
            {isRegister && (
              <TextField
                label="Повторите пароль"
                type="password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                fullWidth
              />
            )}
            {!isRegister && (
              <FormControlLabel
                control={<Checkbox checked={rememberMe} onChange={(e) => setRememberMe(e.target.checked)} />}
                label="Оставаться в системе"
              />
            )}
            {error && <Alert severity="error">{error}</Alert>}
            {success && <Alert severity="success">{success}</Alert>}
            <Button type="submit" variant="contained" size="large" disabled={loading}>
              {loading ? (isRegister ? 'Создаем аккаунт…' : 'Входим…') : isRegister ? 'Создать аккаунт' : 'Войти'}
            </Button>
          </Stack>
        </Box>
      </CardContent>
    </Card>
  );
}
