import { useState } from 'react';
import { Alert, Button, MenuItem, Stack, TextField, Typography, useMediaQuery, useTheme } from '@mui/material';
import {
  createQuickDepositInitialState,
  normalizeQuickDepositState,
  validateQuickDepositAmount,
} from './quick-deposit-domain';
import { Currency } from './types';

interface Props {
  onSubmit: (payload: { amount: number; currency: string; note: string }) => Promise<void>;
  currencies: Currency[];
  defaultCurrencyId?: string;
  disabled?: boolean;
}

export function QuickDeposit({ onSubmit, currencies, defaultCurrencyId, disabled }: Props) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'));
  const initialState = createQuickDepositInitialState(defaultCurrencyId, currencies);
  const currencyOptions = currencies.length ? currencies : [{ id: initialState.currency, name: initialState.currency }];
  const [amount, setAmount] = useState(initialState.amount);
  const [currency, setCurrency] = useState(initialState.currency);
  const [note, setNote] = useState(initialState.note);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async () => {
    const amountError = validateQuickDepositAmount(amount);
    if (amountError) {
      setError(amountError);
      return;
    }

    setError('');
    setLoading(true);
    try {
      await onSubmit(normalizeQuickDepositState({ amount, currency, note }));
    } catch (err) {
      console.error(err);
      setError('Не удалось добавить операцию');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Stack spacing={1.5}>
      <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
        Быстрый ввод средств
      </Typography>
      <TextField
        size="small"
        label="Сумма"
        type="number"
        value={amount}
        onChange={(e) => setAmount(Number(e.target.value))}
        slotProps={{ htmlInput: { min: 0, inputMode: 'decimal' } }}
        helperText="Сумма пополнения счета"
      />
      <TextField
        select
        size="small"
        label="Валюта"
        value={currency}
        onChange={(e) => setCurrency(e.target.value)}
      >
        {currencyOptions.map((item) => (
          <MenuItem key={item.id} value={item.id}>
            {item.id}
          </MenuItem>
        ))}
      </TextField>
      <TextField size="small" label="Комментарий" value={note} onChange={(e) => setNote(e.target.value)} />
      {error && <Alert severity="error">{error}</Alert>}
      <Button variant="contained" onClick={handleSubmit} disabled={disabled || loading} fullWidth={isMobile}>
        {loading ? 'Сохраняем…' : 'Добавить депозит'}
      </Button>
    </Stack>
  );
}
