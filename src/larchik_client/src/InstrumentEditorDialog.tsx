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
import {
  createInstrumentEditorInitialModel,
  INSTRUMENT_TYPE_OPTIONS,
  normalizeInstrumentEditorModel,
  PRICE_SOURCE_OPTIONS,
  requiresInstrumentIsin,
} from './instrument-domain';
import { Category, Currency, Instrument, InstrumentModel, InstrumentType, PriceSource, ReferenceItem } from './types';

interface Props {
  open: boolean;
  initial?: Instrument | null;
  categories: Category[];
  currencies: Currency[];
  countries: ReferenceItem[];
  exchanges: ReferenceItem[];
  submitting?: boolean;
  onClose: () => void;
  onSubmit: (model: InstrumentModel) => Promise<void>;
}

export function InstrumentEditorDialog({
  open,
  initial,
  categories,
  currencies,
  countries,
  exchanges,
  submitting = false,
  onClose,
  onSubmit,
}: Props) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'));
  const [form, setForm] = useState<InstrumentModel>(() => createInstrumentEditorInitialModel(initial, categories, currencies));

  const isValid = useMemo(() => {
    return (
      form.name.trim().length > 0 &&
      form.ticker.trim().length > 0 &&
      (!requiresInstrumentIsin(form.type) || (form.isin?.trim().length ?? 0) > 0) &&
      form.currencyId.trim().length > 0 &&
      form.categoryId > 0
    );
  }, [form]);

  const update = (key: keyof InstrumentModel, value: string | number | boolean | InstrumentType | PriceSource | null | undefined) => {
    setForm((prev) => ({ ...prev, [key]: value }));
  };

  const handleSubmit = async () => {
    await onSubmit(normalizeInstrumentEditorModel(form));
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
              required={requiresInstrumentIsin(form.type)}
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
              {INSTRUMENT_TYPE_OPTIONS.map((option) => (
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
              select
              label="Биржа"
              value={form.exchange ?? ''}
              onChange={(e) => update('exchange', e.target.value)}
              fullWidth
            >
              <MenuItem value="">Не выбрана</MenuItem>
              {exchanges.map((exchange) => (
                <MenuItem key={exchange.id} value={exchange.id}>
                  {exchange.id} - {exchange.name}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              select
              label="Страна"
              value={form.country ?? ''}
              onChange={(e) => update('country', e.target.value)}
              fullWidth
            >
              <MenuItem value="">Не выбрана</MenuItem>
              {countries.map((country) => (
                <MenuItem key={country.id} value={country.id}>
                  {country.id} - {country.name}
                </MenuItem>
              ))}
            </TextField>
          </Stack>
          <TextField
            select
            label="Источник цен"
            value={form.priceSource ?? ''}
            onChange={(e) => update('priceSource', e.target.value ? (e.target.value as PriceSource) : null)}
            disabled={!form.isTrading}
            fullWidth
          >
            <MenuItem value="">Не синхронизировать</MenuItem>
            {PRICE_SOURCE_OPTIONS.map((option) => (
              <MenuItem key={option.value} value={option.value}>
                {option.label}
              </MenuItem>
            ))}
          </TextField>
          <FormControlLabel
            control={
              <Checkbox
                checked={form.isTrading}
                onChange={(_, checked) => {
                  setForm((prev) => ({
                    ...prev,
                    isTrading: checked,
                    priceSource: checked ? prev.priceSource : null,
                  }));
                }}
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
