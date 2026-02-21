import { useState } from 'react';
import { Alert, Box, Button, Card, CardContent, Stack, TextField, Typography } from '@mui/material';

interface Props {
  onSubmit: (email: string, password: string) => Promise<void>;
}

export function AuthForm({ onSubmit }: Props) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await onSubmit(email, password);
    } catch (err) {
      console.error(err);
      setError('Не удалось войти. Проверьте email/пароль.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Card sx={{ maxWidth: 440, mx: 'auto', p: 1 }}>
      <CardContent>
        <Typography variant="h5" fontWeight={700} gutterBottom>
          Larchik Investments
        </Typography>
        <Typography variant="body2" color="text.secondary" gutterBottom>
          Управляйте портфелями, следите за P&L и потоками в одной панели.
        </Typography>
        <Box component="form" onSubmit={handleSubmit} sx={{ mt: 2 }}>
          <Stack spacing={2}>
            <TextField
              label="Email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              autoFocus
              fullWidth
            />
            <TextField
              label="Пароль"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              fullWidth
            />
            {error && <Alert severity="error">{error}</Alert>}
            <Button type="submit" variant="contained" size="large" disabled={loading}>
              {loading ? 'Входим…' : 'Войти'}
            </Button>
          </Stack>
        </Box>
      </CardContent>
    </Card>
  );
}
