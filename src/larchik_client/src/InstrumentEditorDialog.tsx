import { useMemo, useState } from 'react';
import {
  Button,
  Checkbox,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  MenuItem,
  Stack,
  TextField,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import { Category, Currency, Instrument, InstrumentModel, InstrumentType } from './types';

const INSTRUMENT_TYPES: { value: InstrumentType; label: string }[] = [
  { value: 'Equity', label: 'Акция' },
  { value: 'Bond', label: 'Облигация' },
  { value: 'Etf', label: 'ETF' },
  { value: 'Currency', label: 'Валюта' },
  { value: 'Commodity', label: 'Товар' },
  { value: 'Crypto', label: 'Крипто' },
];

function createInitialForm(initial?: Instrument | null, categories: Category[] = [], currencies: Currency[] = []): InstrumentModel {
  return {
    name: initial?.name ?? '',
    ticker: initial?.ticker ?? '',
    isin: initial?.isin ?? '',
    figi: initial?.figi ?? '',
    type: initial?.type ?? 'Equity',
    currencyId: initial?.currencyId ?? currencies[0]?.id ?? 'USD',
    categoryId: initial?.categoryId ?? categories[0]?.id ?? 0,
    exchange: initial?.exchange ?? '',
    country: initial?.country ?? '',
    isTrading: initial?.isTrading ?? true,
  };
}

interface Props {
  open: boolean;
  initial?: Instrument | null;
  categories: Category[];
  currencies: Currency[];
  submitting?: boolean;
  onClose: () => void;
  onSubmit: (model: InstrumentModel) => Promise<void>;
}

export function InstrumentEditorDialog({ open, initial, categories, currencies, submitting = false, onClose, onSubmit }: Props) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'));
  const [form, setForm] = useState<InstrumentModel>(() => createInitialForm(initial, categories, currencies));

  const isValid = useMemo(() => {
    return (
      form.name.trim().length > 0 &&
      form.ticker.trim().length > 0 &&
      form.isin.trim().length > 0 &&
      form.currencyId.trim().length > 0 &&
      form.categoryId > 0
    );
  }, [form]);

  const update = (key: keyof InstrumentModel, value: string | number | boolean | InstrumentType | undefined) => {
    setForm((prev) => ({ ...prev, [key]: value }));
  };

  const handleSubmit = async () => {
    await onSubmit({
      name: form.name.trim(),
      ticker: form.ticker.trim().toUpperCase(),
      isin: form.isin.trim().toUpperCase(),
      figi: form.figi?.trim() ? form.figi.trim().toUpperCase() : undefined,
      type: form.type,
      currencyId: form.currencyId.trim().toUpperCase(),
      categoryId: form.categoryId,
      exchange: form.exchange?.trim() || undefined,
      country: form.country?.trim() || undefined,
      isTrading: form.isTrading,
    });
  };

  return (
    <Dialog open={open} onClose={submitting ? undefined : onClose} fullWidth maxWidth="sm" fullScreen={isMobile} scroll="paper">
      <DialogTitle>{initial ? 'Редактировать инструмент' : 'Новый инструмент'}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField
            label="Название"
            value={form.name}
            onChange={(e) => update('name', e.target.value)}
            required
            fullWidth
          />
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
            <TextField
              label="Тикер"
              value={form.ticker}
              onChange={(e) => update('ticker', e.target.value)}
              required
              fullWidth
            />
            <TextField
              label="ISIN"
              value={form.isin}
              onChange={(e) => update('isin', e.target.value)}
              required
              fullWidth
            />
          </Stack>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
            <TextField
              label="FIGI"
              value={form.figi ?? ''}
              onChange={(e) => update('figi', e.target.value)}
              fullWidth
            />
            <TextField
              select
              label="Валюта"
              value={form.currencyId}
              onChange={(e) => update('currencyId', e.target.value)}
              required
              fullWidth
            >
              {currencies.map((currency) => (
                <MenuItem key={currency.id} value={currency.id}>
                  {currency.id} - {currency.name}
                </MenuItem>
              ))}
            </TextField>
          </Stack>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
            <TextField
              select
              label="Тип"
              value={form.type}
              onChange={(e) => update('type', e.target.value as InstrumentType)}
              fullWidth
            >
              {INSTRUMENT_TYPES.map((option) => (
                <MenuItem key={option.value} value={option.value}>
                  {option.label}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              select
              label="Категория"
              value={form.categoryId > 0 ? form.categoryId : ''}
              onChange={(e) => update('categoryId', Number(e.target.value))}
              required
              fullWidth
            >
              {categories.map((category) => (
                <MenuItem key={category.id} value={category.id}>
                  {category.name}
                </MenuItem>
              ))}
            </TextField>
          </Stack>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
            <TextField
              label="Биржа"
              value={form.exchange ?? ''}
              onChange={(e) => update('exchange', e.target.value)}
              fullWidth
            />
            <TextField
              label="Страна"
              value={form.country ?? ''}
              onChange={(e) => update('country', e.target.value)}
              fullWidth
            />
          </Stack>
          <FormControlLabel
            control={
              <Checkbox
                checked={form.isTrading}
                onChange={(_, checked) => update('isTrading', checked)}
              />
            }
            label="Торгуется"
          />
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={submitting}>
          Отмена
        </Button>
        <Button
          variant="contained"
          onClick={() => void handleSubmit()}
          disabled={!isValid || submitting || categories.length === 0 || currencies.length === 0}
        >
          {initial ? 'Сохранить' : 'Создать'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
