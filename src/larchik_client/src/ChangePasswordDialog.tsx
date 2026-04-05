import { useEffect, useState } from 'react';
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField, Typography } from '@mui/material';
import { api } from './api';

interface Props {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

function getPasswordErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof Error)) return fallback;

  try {
    const payload = JSON.parse(error.message) as
      | {
          title?: string;
          errors?: Array<{ description?: string }> | Record<string, string[]>;
          message?: string;
        }
      | Array<{ description?: string }>;

    if (Array.isArray(payload)) {
      const identityErrors = payload.map((item) => item.description).filter(Boolean);
      if (identityErrors.length > 0) {
        return identityErrors[0]!;
      }
    } else if (Array.isArray(payload.errors)) {
      const identityErrors = payload.errors.map((item) => item.description).filter(Boolean);
      if (identityErrors.length > 0) {
        return identityErrors[0]!;
      }
    } else if (payload.errors) {
      const validationErrors = Object.values(payload.errors)
        .flat()
        .filter(Boolean);

      if (validationErrors.length > 0) {
        return validationErrors[0];
      }
    }

    if (!Array.isArray(payload)) {
      return payload.message || payload.title || error.message || fallback;
    }
  } catch {
    return error.message || fallback;
  }

  return error.message || fallback;
}

export function ChangePasswordDialog({ open, onClose, onSuccess }: Props) {
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!open) {
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      setError('');
      setLoading(false);
    }
  }, [open]);

  const handleSubmit = async () => {
    setError('');

    if (newPassword !== confirmPassword) {
      setError('Новый пароль и подтверждение должны совпадать.');
      return;
    }

    setLoading(true);

    try {
      await api.changePassword(currentPassword, newPassword);
      onClose();
      onSuccess();
    } catch (err) {
      setError(getPasswordErrorMessage(err, 'Не удалось сменить пароль.'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={open} onClose={loading ? undefined : onClose} fullWidth maxWidth="xs">
      <DialogTitle>Сменить пароль</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          <Typography variant="body2" color="text.secondary">
            Используйте пароль минимум из 8 символов, с заглавной, строчной буквой и цифрой.
          </Typography>
          <TextField
            label="Текущий пароль"
            type="password"
            value={currentPassword}
            onChange={(e) => setCurrentPassword(e.target.value)}
            fullWidth
            autoFocus
          />
          <TextField
            label="Новый пароль"
            type="password"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            fullWidth
          />
          <TextField
            label="Повторите новый пароль"
            type="password"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            fullWidth
          />
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={loading}>
          Отмена
        </Button>
        <Button onClick={handleSubmit} variant="contained" disabled={loading}>
          {loading ? 'Сохраняем…' : 'Сменить пароль'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
