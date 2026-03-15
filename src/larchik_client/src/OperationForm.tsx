import { useEffect, useMemo, useRef, useState } from 'react';
import {
  Alert,
  Autocomplete,
  Button,
  CircularProgress,
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
import { InstrumentLookup, OperationModel, OperationType } from './types';

const TYPE_OPTIONS: { value: OperationType; label: string }[] = [
  { value: 'Buy', label: 'Покупка' },
  { value: 'Sell', label: 'Продажа' },
  { value: 'Dividend', label: 'Дивиденд' },
  { value: 'Fee', label: 'Комиссия' },
  { value: 'Deposit', label: 'Депозит' },
  { value: 'Withdraw', label: 'Вывод' },
  { value: 'TransferIn', label: 'Перевод в' },
  { value: 'TransferOut', label: 'Перевод из' },
  { value: 'BondPartialRedemption', label: 'Частичное погашение облигации' },
  { value: 'BondMaturity', label: 'Полное погашение облигации' },
  { value: 'Split', label: 'Сплит' },
  { value: 'ReverseSplit', label: 'Обратный сплит' },
  { value: 'CashAdjustment', label: 'Движение денег' },
];

const INSTRUMENT_OPERATION_TYPES = new Set<OperationType>([
  'Buy',
  'Sell',
  'BondPartialRedemption',
  'BondMaturity',
  'Split',
  'ReverseSplit',
]);

interface Props {
  open: boolean;
  initial?: Partial<OperationModel>;
  onClose: () => void;
  onSubmit: (model: OperationModel) => Promise<void>;
  searchInstruments: (query: string) => Promise<InstrumentLookup[]>;
}

const todayIso = () => new Date().toISOString().slice(0, 10);
const DATE_ONLY_REGEX = /^\d{4}-\d{2}-\d{2}$/;

const toDateInputValue = (value?: string) => (value ? value.slice(0, 10) : '');

const toUtcIso = (value?: string): string | undefined => {
  if (!value) return undefined;
  if (DATE_ONLY_REGEX.test(value)) return `${value}T00:00:00.000Z`;

  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? undefined : parsed.toISOString();
};

const createInitialForm = (initial?: Partial<OperationModel>): OperationModel => ({
  instrumentId: initial?.instrumentId,
  type: (initial?.type as OperationType) ?? 'Buy',
  quantity: initial?.quantity ?? 0,
  price: initial?.price ?? 0,
  fee: initial?.fee ?? 0,
  currencyId: initial?.currencyId ?? 'RUB',
  tradeDate: toDateInputValue(initial?.tradeDate) || todayIso(),
  settlementDate: toDateInputValue(initial?.settlementDate) || undefined,
  note: initial?.note,
});

export function OperationForm({ open, initial, onClose, onSubmit, searchInstruments }: Props) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'));
  const [form, setForm] = useState<OperationModel>(() => createInitialForm(initial));
  const [saving, setSaving] = useState(false);
  const [instrumentOptions, setInstrumentOptions] = useState<InstrumentLookup[]>([]);
  const [selectedInstrument, setSelectedInstrument] = useState<InstrumentLookup | null>(null);
  const [instrumentSearch, setInstrumentSearch] = useState('');
  const [instrumentLoading, setInstrumentLoading] = useState(false);
  const [instrumentError, setInstrumentError] = useState<string | null>(null);
  const suppressNextInstrumentSearchRef = useRef(false);

  useEffect(() => {
    if (!open) return;
    setForm(createInitialForm(initial));
    setSelectedInstrument(null);
    setInstrumentSearch('');
    setInstrumentOptions([]);
    setInstrumentError(null);
  }, [open, initial]);

  useEffect(() => {
    if (!open) return;

    const query = instrumentSearch.trim();
    if (suppressNextInstrumentSearchRef.current) {
      suppressNextInstrumentSearchRef.current = false;
      setInstrumentLoading(false);
      setInstrumentError(null);
      return;
    }

    if (!query) {
      setInstrumentOptions([]);
      setInstrumentLoading(false);
      setInstrumentError(null);
      return;
    }

    let canceled = false;
    const timer = window.setTimeout(async () => {
      setInstrumentLoading(true);
      setInstrumentError(null);
      try {
        const result = await searchInstruments(query);
        if (!canceled) {
          setInstrumentOptions(result);
        }
      } catch {
        if (!canceled) {
          setInstrumentOptions([]);
          setInstrumentError('Не удалось загрузить инструменты.');
        }
      } finally {
        if (!canceled) {
          setInstrumentLoading(false);
        }
      }
    }, 250);

    return () => {
      canceled = true;
      window.clearTimeout(timer);
    };
  }, [instrumentSearch, open, searchInstruments]);

  const isSplitType = form.type === 'Split' || form.type === 'ReverseSplit';
  const isInstrumentType = INSTRUMENT_OPERATION_TYPES.has(form.type);

  const selectedInstrumentValue = useMemo(() => {
    if (!form.instrumentId) return null;

    if (selectedInstrument?.id === form.instrumentId) return selectedInstrument;
    return instrumentOptions.find((x) => x.id === form.instrumentId) ?? null;
  }, [form.instrumentId, instrumentOptions, selectedInstrument]);

  const update = (key: keyof OperationModel, value: string | number | undefined) => {
    setForm((prev) => ({ ...prev, [key]: value }));
  };

  const handleSubmit = async () => {
    setSaving(true);
    try {
      const tradeDate = toUtcIso(form.tradeDate);
      const settlementDate = toUtcIso(form.settlementDate);

      await onSubmit({
        ...form,
        instrumentId: form.instrumentId || undefined,
        tradeDate: tradeDate ?? form.tradeDate,
        settlementDate,
        note: form.note || undefined,
      });
      onClose();
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm" fullScreen={isMobile} scroll="paper">
      <DialogTitle>{initial ? 'Редактировать операцию' : 'Новая операция'}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField
            select
            label="Тип"
            value={form.type}
            onChange={(e) => update('type', e.target.value as OperationType)}
          >
            {TYPE_OPTIONS.map((t) => (
              <MenuItem key={t.value} value={t.value}>
                {t.label}
              </MenuItem>
            ))}
          </TextField>

          <Autocomplete<InstrumentLookup, false, false, false>
            options={instrumentOptions}
            value={selectedInstrumentValue}
            inputValue={instrumentSearch}
            loading={instrumentLoading}
            filterOptions={(options) => options}
            isOptionEqualToValue={(option, value) => option.id === value.id}
            onInputChange={(_, value, reason) => {
              if (reason === 'input') {
                setInstrumentSearch(value);
                update('instrumentId', undefined);
                setSelectedInstrument(null);
              }

              if (reason === 'clear') {
                setInstrumentSearch('');
                update('instrumentId', undefined);
                setSelectedInstrument(null);
              }
            }}
            onChange={(_, value) => {
              update('instrumentId', value?.id ?? undefined);
              setSelectedInstrument(value);
              suppressNextInstrumentSearchRef.current = true;
              setInstrumentSearch(value?.ticker ?? '');
            }}
            getOptionLabel={(option) => `${option.ticker} - ${option.name}${option.currencyId ? ` (${option.currencyId})` : ''}`}
            noOptionsText={instrumentSearch ? 'Ничего не найдено' : 'Начните вводить тикер'}
            renderOption={(props, option) => (
              <li {...props} key={option.id}>
                {option.ticker} - {option.name} ({option.currencyId})
              </li>
            )}
            renderInput={(params) => (
              <TextField
                {...params}
                label="Инструмент (тикер)"
                helperText={
                  isInstrumentType
                    ? 'Выберите инструмент из списка по тикеру'
                    : 'Для кэш-операций оставьте пустым'
                }
                error={isInstrumentType && !form.instrumentId}
                InputProps={{
                  ...params.InputProps,
                  endAdornment: (
                    <>
                      {instrumentLoading ? <CircularProgress color="inherit" size={16} /> : null}
                      {params.InputProps.endAdornment}
                    </>
                  ),
                }}
              />
            )}
          />

          {instrumentError && <Alert severity="warning">{instrumentError}</Alert>}

          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
            <TextField
              label={isSplitType ? 'Коэффициент' : 'Количество'}
              type="number"
              value={form.quantity}
              onChange={(e) => update('quantity', Number(e.target.value))}
              helperText={isSplitType ? '1:10 = 10, 10:1 = 0.1' : undefined}
              fullWidth
            />
            <TextField
              label={isSplitType ? 'Цена/сумма (0)' : 'Цена/сумма'}
              type="number"
              value={form.price}
              onChange={(e) => update('price', Number(e.target.value))}
              fullWidth
            />
          </Stack>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
            <TextField
              label={isSplitType ? 'Комиссия (0)' : 'Комиссия'}
              type="number"
              value={form.fee}
              onChange={(e) => update('fee', Number(e.target.value))}
              fullWidth
            />
            <TextField
              label="Валюта"
              value={form.currencyId}
              onChange={(e) => update('currencyId', e.target.value.toUpperCase())}
              fullWidth
            />
          </Stack>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
            <TextField
              label="Дата сделки"
              type="date"
              value={toDateInputValue(form.tradeDate)}
              onChange={(e) => update('tradeDate', e.target.value)}
              InputLabelProps={{ shrink: true }}
              fullWidth
            />
            <TextField
              label="Дата расчёта"
              type="date"
              value={toDateInputValue(form.settlementDate)}
              onChange={(e) => update('settlementDate', e.target.value || undefined)}
              InputLabelProps={{ shrink: true }}
              fullWidth
            />
          </Stack>
          <TextField
            label="Комментарий"
            value={form.note ?? ''}
            onChange={(e) => update('note', e.target.value)}
            multiline
            rows={2}
          />
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
        <Button onClick={onClose} fullWidth={isMobile}>
          Отмена
        </Button>
        <Button onClick={handleSubmit} variant="contained" disabled={saving} fullWidth={isMobile}>
          {saving ? 'Сохраняем…' : 'Сохранить'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
