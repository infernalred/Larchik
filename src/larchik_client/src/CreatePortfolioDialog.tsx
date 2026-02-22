import { useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Autocomplete,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Stack,
  TextField,
  useMediaQuery,
  useTheme,
} from '@mui/material';
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
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'));
  const [form, setForm] = useState<PortfolioForm>({
    name: '',
    brokerId: '',
    reportingCurrencyId: 'RUB',
  });

  useEffect(() => {
    if (!open) return;
    setForm({
      name: '',
      brokerId: '',
      reportingCurrencyId: 'RUB',
    });
  }, [open]);

  const canSubmit = useMemo(
    () => form.name.trim().length > 0 && form.brokerId.length > 0 && !submitting,
    [form.brokerId, form.name, submitting],
  );
  const selectedBroker = useMemo(
    () => brokers.find((x) => x.id === form.brokerId) ?? null,
    [brokers, form.brokerId],
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
    <Dialog
      open={open}
      onClose={submitting ? undefined : onClose}
      fullWidth
      maxWidth="sm"
      fullScreen={isMobile}
      scroll="paper"
    >
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
          <Autocomplete
            options={brokers}
            value={selectedBroker}
            onChange={(_, value) => update('brokerId', value?.id ?? '')}
            getOptionLabel={(option) => option.name}
            isOptionEqualToValue={(option, value) => option.id === value.id}
            disabled={submitting || !brokers.length}
            noOptionsText="Ничего не найдено"
            renderInput={(params) => (
              <TextField
                {...params}
                label="Брокер"
                placeholder="Начните вводить название"
              />
            )}
            fullWidth
          />
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
      <DialogActions
        sx={{
          px: { xs: 2, sm: 3 },
          pb: { xs: 2, sm: 1.5 },
          pt: 1,
          flexDirection: { xs: 'column', sm: 'row' },
          gap: 1,
        }}
      >
        <Button onClick={onClose} disabled={submitting} fullWidth={isMobile}>
          Отмена
        </Button>
        <Button onClick={handleSubmit} variant="contained" disabled={!canSubmit} fullWidth={isMobile}>
          {submitting ? 'Создаем…' : 'Создать'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
