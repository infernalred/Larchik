import { useEffect, useMemo, useState } from 'react';
import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Paper,
  Stack,
  TextField,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import { CorporateActionType, Instrument, InstrumentCorporateAction, InstrumentCorporateActionModel } from './types';

const ACTION_TYPES: { value: CorporateActionType; label: string }[] = [
  { value: 'Split', label: 'Сплит' },
  { value: 'ReverseSplit', label: 'Обратный сплит' },
];

const todayIso = () => new Date().toISOString().slice(0, 10);

function createInitialForm(action?: InstrumentCorporateAction | null): InstrumentCorporateActionModel {
  return {
    type: action?.type ?? 'Split',
    factor: action?.factor ?? 2,
    effectiveDate: action?.effectiveDate?.slice(0, 10) ?? todayIso(),
    note: action?.note ?? '',
  };
}

function toUtcIso(value: string): string {
  return `${value}T00:00:00.000Z`;
}

interface Props {
  open: boolean;
  instrument: Instrument | null;
  items: InstrumentCorporateAction[];
  loading?: boolean;
  submitting?: boolean;
  onClose: () => void;
  onCreate: (model: InstrumentCorporateActionModel) => Promise<void>;
  onUpdate: (id: string, model: InstrumentCorporateActionModel) => Promise<void>;
  onDelete: (id: string) => Promise<void>;
}

export function InstrumentCorporateActionsDialog({
  open,
  instrument,
  items,
  loading = false,
  submitting = false,
  onClose,
  onCreate,
  onUpdate,
  onDelete,
}: Props) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'));
  const [editing, setEditing] = useState<InstrumentCorporateAction | null>(null);
  const [form, setForm] = useState<InstrumentCorporateActionModel>(() => createInitialForm());

  useEffect(() => {
    if (!open) {
      return;
    }

    setEditing(null);
    setForm(createInitialForm());
  }, [open, instrument?.id]);

  const isValid = useMemo(() => {
    return form.factor > 0 && form.factor !== 1 && form.effectiveDate.length > 0 && form.note.trim().length > 0;
  }, [form]);

  const update = (key: keyof InstrumentCorporateActionModel, value: string | number | CorporateActionType) => {
    setForm((prev) => ({ ...prev, [key]: value }));
  };

  const startEdit = (item: InstrumentCorporateAction) => {
    setEditing(item);
    setForm(createInitialForm(item));
  };

  const resetForm = () => {
    setEditing(null);
    setForm(createInitialForm());
  };

  const handleSubmit = async () => {
    const model: InstrumentCorporateActionModel = {
      type: form.type,
      factor: form.factor,
      effectiveDate: toUtcIso(form.effectiveDate),
      note: form.note.trim(),
    };

    if (editing) {
      await onUpdate(editing.id, model);
    } else {
      await onCreate(model);
    }

    resetForm();
  };

  const title = instrument ? `Корпоративные действия: ${instrument.ticker}` : 'Корпоративные действия';

  return (
    <Dialog open={open} onClose={submitting ? undefined : onClose} fullWidth maxWidth="md" fullScreen={isMobile} scroll="paper">
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Paper variant="outlined" sx={{ p: 2, backgroundImage: 'none' }}>
            <Stack spacing={2}>
              <Typography sx={{ fontWeight: 700 }}>
                {editing ? 'Редактировать действие' : 'Новое действие'}
              </Typography>
              <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
                <TextField
                  select
                  label="Тип"
                  value={form.type}
                  onChange={(e) => update('type', e.target.value as CorporateActionType)}
                  fullWidth
                >
                  {ACTION_TYPES.map((option) => (
                    <MenuItem key={option.value} value={option.value}>
                      {option.label}
                    </MenuItem>
                  ))}
                </TextField>
                <TextField
                  label="Коэффициент"
                  type="number"
                  value={form.factor}
                  onChange={(e) => update('factor', Number(e.target.value))}
                  helperText="2 = 1:2, 0.1 = 10:1"
                  fullWidth
                />
                <TextField
                  label="Дата вступления в силу"
                  type="date"
                  value={form.effectiveDate}
                  onChange={(e) => update('effectiveDate', e.target.value)}
                  slotProps={{ inputLabel: { shrink: true } }}
                  fullWidth
                />
              </Stack>
              <TextField
                label="Комментарий"
                value={form.note}
                onChange={(e) => update('note', e.target.value)}
                multiline
                rows={2}
                fullWidth
              />
              <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
                <Button variant="contained" onClick={() => void handleSubmit()} disabled={!isValid || submitting}>
                  {editing ? 'Сохранить' : 'Добавить'}
                </Button>
                {editing && (
                  <Button onClick={resetForm} disabled={submitting}>
                    Отмена редактирования
                  </Button>
                )}
              </Stack>
            </Stack>
          </Paper>

          <Stack spacing={1.25}>
            {items.length === 0 && !loading ? (
              <Typography color="text.secondary">
                Для этого инструмента еще нет административных сплитов.
              </Typography>
            ) : null}

            {items.map((item) => (
              <Paper key={item.id} variant="outlined" sx={{ p: 1.5, backgroundImage: 'none' }}>
                <Stack
                  direction={{ xs: 'column', md: 'row' }}
                  spacing={1.5}
                  sx={{ justifyContent: 'space-between', alignItems: { xs: 'stretch', md: 'center' } }}
                >
                  <Stack spacing={0.5}>
                    <Typography sx={{ fontWeight: 700 }}>
                      {item.type === 'ReverseSplit' ? 'Обратный сплит' : 'Сплит'} · {item.factor}
                    </Typography>
                    <Typography variant="body2" color="text.secondary">
                      Дата: {item.effectiveDate.slice(0, 10)}
                    </Typography>
                    <Typography variant="body2">{item.note}</Typography>
                  </Stack>
                  <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
                    <Button variant="outlined" onClick={() => startEdit(item)} disabled={submitting}>
                      Изменить
                    </Button>
                    <Button
                      variant="outlined"
                      color="error"
                      onClick={() => void onDelete(item.id)}
                      disabled={submitting}
                    >
                      Удалить
                    </Button>
                  </Stack>
                </Stack>
              </Paper>
            ))}
          </Stack>
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} disabled={submitting}>
          Закрыть
        </Button>
      </DialogActions>
    </Dialog>
  );
}
