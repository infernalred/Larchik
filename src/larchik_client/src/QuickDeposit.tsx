import { useState } from 'react';
import { Alert, Button, Stack, TextField, Typography, useMediaQuery, useTheme } from '@mui/material';

interface Props {
  onSubmit: (payload: { amount: number; currency: string; note: string }) => Promise<void>;
  disabled?: boolean;
}

export function QuickDeposit({ onSubmit, disabled }: Props) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'));
  const [amount, setAmount] = useState(100000);
  const [currency, setCurrency] = useState('RUB');
  const [note, setNote] = useState('Ввод средств');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async () => {
    if (amount <= 0) {
      setError('Введите сумму больше нуля');
      return;
    }
    setError('');
    setLoading(true);
    try {
      await onSubmit({ amount, currency, note });
    } catch (err) {
      console.error(err);
      setError('Не удалось добавить операцию');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Stack spacing={1.5}>
      <Typography variant="subtitle1" fontWeight={700}>
        Быстрый ввод средств
      </Typography>
      <TextField
        size="small"
        label="Сумма"
        type="number"
        value={amount}
        onChange={(e) => setAmount(Number(e.target.value))}
        inputProps={{ min: 0, inputMode: 'decimal' }}
        helperText="Сумма пополнения счета"
      />
      <TextField
        size="small"
        label="Валюта"
        value={currency}
        onChange={(e) => setCurrency(e.target.value.toUpperCase())}
        inputProps={{ maxLength: 5 }}
      />
      <TextField size="small" label="Комментарий" value={note} onChange={(e) => setNote(e.target.value)} />
      {error && <Alert severity="error">{error}</Alert>}
      <Button variant="contained" onClick={handleSubmit} disabled={disabled || loading} fullWidth={isMobile}>
        {loading ? 'Сохраняем…' : 'Добавить депозит'}
      </Button>
    </Stack>
  );
}
