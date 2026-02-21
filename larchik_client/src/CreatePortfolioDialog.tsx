import { useEffect, useMemo, useState } from 'react';
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, Stack, TextField } from '@mui/material';
import { Broker } from './types';

const CURRENCIES = ['RUB', 'USD', 'EUR'];

interface PortfolioForm {
  name: string;
  brokerId: string;
  reportingCurrencyId: string;
}

interface Props {
  open: boolean;
  brokers: Broker[];
  submitting: boolean;
  error?: string;
  onClose: () => void;
  onSubmit: (form: PortfolioForm) => Promise<void>;
}

export function CreatePortfolioDialog({ open, brokers, submitting, error, onClose, onSubmit }: Props) {
  const [form, setForm] = useState<PortfolioForm>({
    name: '',
    brokerId: '',
    reportingCurrencyId: 'RUB',
  });

  useEffect(() => {
    if (!open) return;
    setForm({
      name: '',
      brokerId: brokers[0]?.id ?? '',
      reportingCurrencyId: 'RUB',
    });
  }, [open, brokers]);

  const canSubmit = useMemo(
    () => form.name.trim().length > 0 && form.brokerId.length > 0 && !submitting,
    [form.brokerId, form.name, submitting],
  );

  const update = <K extends keyof PortfolioForm>(key: K, value: PortfolioForm[K]) => {
    setForm((prev) => ({ ...prev, [key]: value }));
  };

  const handleSubmit = async () => {
    if (!canSubmit) return;
    await onSubmit({
      name: form.name.trim(),
      brokerId: form.brokerId,
      reportingCurrencyId: form.reportingCurrencyId,
    });
  };

  return (
    <Dialog open={open} onClose={submitting ? undefined : onClose} fullWidth maxWidth="sm">
      <DialogTitle>Новый счет</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          {error && <Alert severity="error">{error}</Alert>}
          {!brokers.length && (
            <Alert severity="warning">В базе нет брокеров. Добавьте брокера и повторите создание счета.</Alert>
          )}
          <TextField
            label="Название счета"
            value={form.name}
            onChange={(e) => update('name', e.target.value)}
            disabled={submitting}
            autoFocus
            fullWidth
          />
          <TextField
            select
            label="Брокер"
            value={form.brokerId}
            onChange={(e) => update('brokerId', e.target.value)}
            disabled={submitting || !brokers.length}
            fullWidth
          >
            {brokers.map((broker) => (
              <MenuItem key={broker.id} value={broker.id}>
                {broker.name}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            select
            label="Базовая валюта"
            value={form.reportingCurrencyId}
            onChange={(e) => update('reportingCurrencyId', e.target.value)}
            disabled={submitting}
            fullWidth
          >
            {CURRENCIES.map((currency) => (
              <MenuItem key={currency} value={currency}>
                {currency}
              </MenuItem>
            ))}
          </TextField>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={submitting}>
          Отмена
        </Button>
        <Button onClick={handleSubmit} variant="contained" disabled={!canSubmit}>
          {submitting ? 'Создаем…' : 'Создать'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
