import { useState } from 'react';
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Stack,
  TextField,
} from '@mui/material';
import { OperationModel, OperationType } from './types';

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
];

interface Props {
  open: boolean;
  initial?: Partial<OperationModel>;
  onClose: () => void;
  onSubmit: (model: OperationModel) => Promise<void>;
}

const todayIso = () => new Date().toISOString().slice(0, 10);

export function OperationForm({ open, initial, onClose, onSubmit }: Props) {
  const [form, setForm] = useState<OperationModel>(() => ({
    instrumentId: initial?.instrumentId,
    type: (initial?.type as OperationType) ?? 'Buy',
    quantity: initial?.quantity ?? 0,
    price: initial?.price ?? 0,
    fee: initial?.fee ?? 0,
    currencyId: initial?.currencyId ?? 'RUB',
    tradeDate: initial?.tradeDate ?? todayIso(),
    settlementDate: initial?.settlementDate,
    note: initial?.note,
  }));
  const [saving, setSaving] = useState(false);
  const isSplitType = form.type === 'Split' || form.type === 'ReverseSplit';

  const update = (key: keyof OperationModel, value: any) => {
    setForm((prev) => ({ ...prev, [key]: value }));
  };

  const handleSubmit = async () => {
    setSaving(true);
    try {
      await onSubmit({
        ...form,
        instrumentId: form.instrumentId || undefined,
        settlementDate: form.settlementDate || undefined,
        note: form.note || undefined,
      });
      onClose();
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
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
          <TextField
            label="InstrumentId (опционально)"
            value={form.instrumentId ?? ''}
            onChange={(e) => update('instrumentId', e.target.value)}
            helperText="Для кэш-операций оставьте пустым"
          />
          <Stack direction="row" spacing={2}>
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
          <Stack direction="row" spacing={2}>
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
          <Stack direction="row" spacing={2}>
            <TextField
              label="Дата сделки"
              type="date"
              value={form.tradeDate.slice(0, 10)}
              onChange={(e) => update('tradeDate', new Date(e.target.value).toISOString())}
              InputLabelProps={{ shrink: true }}
              fullWidth
            />
            <TextField
              label="Дата расчёта"
              type="date"
              value={form.settlementDate?.slice(0, 10) ?? ''}
              onChange={(e) =>
                update('settlementDate', e.target.value ? new Date(e.target.value).toISOString() : undefined)
              }
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
      <DialogActions>
        <Button onClick={onClose}>Отмена</Button>
        <Button onClick={handleSubmit} variant="contained" disabled={saving}>
          {saving ? 'Сохраняем…' : 'Сохранить'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
