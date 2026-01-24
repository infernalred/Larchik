import { useState } from 'react';
import { Alert, Button, Stack, TextField, Typography } from '@mui/material';

interface Props {
  onSubmit: (payload: { amount: number; currency: string; note: string }) => Promise<void>;
  disabled?: boolean;
}

export function QuickDeposit({ onSubmit, disabled }: Props) {
  const [amount, setAmount] = useState(100000);
  const [currency, setCurrency] = useState('RUB');
  const [note, setNote] = useState('Ввод средств');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async () => {
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
        label="Сумма"
        type="number"
        value={amount}
        onChange={(e) => setAmount(Number(e.target.value))}
        inputProps={{ min: 0 }}
      />
      <TextField
        label="Валюта"
        value={currency}
        onChange={(e) => setCurrency(e.target.value.toUpperCase())}
        inputProps={{ maxLength: 5 }}
      />
      <TextField label="Комментарий" value={note} onChange={(e) => setNote(e.target.value)} />
      {error && <Alert severity="error">{error}</Alert>}
      <Button variant="contained" onClick={handleSubmit} disabled={disabled || loading}>
        {loading ? 'Сохраняем…' : 'Добавить депозит'}
      </Button>
    </Stack>
  );
}
