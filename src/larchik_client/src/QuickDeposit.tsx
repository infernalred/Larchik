import { useState } from 'react';
import { Alert, Box, Button, Grid, MenuItem, TextField, Typography } from '@mui/material';
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
    <Box
      component="form"
      onSubmit={(event) => {
        event.preventDefault();
        void handleSubmit();
      }}
    >
      <Grid container spacing={1.5} sx={{ alignItems: 'center' }}>
        <Grid size={{ xs: 12, lg: 2 }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
            Пополнение счета
          </Typography>
        </Grid>
        <Grid size={{ xs: 12, sm: 6, lg: 2.5 }}>
          <TextField
            fullWidth
            size="small"
            label="Сумма"
            type="number"
            value={amount}
            onChange={(event) => setAmount(Number(event.target.value))}
            slotProps={{ htmlInput: { min: 0, inputMode: 'decimal' } }}
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, lg: 2 }}>
          <TextField
            fullWidth
            select
            size="small"
            label="Валюта"
            value={currency}
            onChange={(event) => setCurrency(event.target.value)}
          >
            {currencyOptions.map((item) => (
              <MenuItem key={item.id} value={item.id}>
                {item.id}
              </MenuItem>
            ))}
          </TextField>
        </Grid>
        <Grid size={{ xs: 12, sm: 8, lg: 3 }}>
          <TextField
            fullWidth
            size="small"
            label="Комментарий"
            value={note}
            onChange={(event) => setNote(event.target.value)}
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 4, lg: 2.5 }}>
          <Button type="submit" variant="contained" disabled={disabled || loading} fullWidth>
            {loading ? 'Сохраняем…' : 'Добавить депозит'}
          </Button>
        </Grid>
        {error && (
          <Grid size={12}>
            <Alert severity="error">{error}</Alert>
          </Grid>
        )}
      </Grid>
    </Box>
  );
}
