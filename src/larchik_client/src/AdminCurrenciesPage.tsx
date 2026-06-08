import { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  Paper,
  Snackbar,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import EditIcon from '@mui/icons-material/Edit';
import RefreshIcon from '@mui/icons-material/Refresh';
import { api } from './api';
import { getApiErrorMessage } from './error-utils';
import { Currency, CurrencyModel, UpdateCurrencyModel } from './types';

interface ToastState {
  open: boolean;
  severity: 'success' | 'error';
  message: string;
}

interface EditorState {
  id: string;
  name: string;
}

function createEmptyForm(): EditorState {
  return { id: '', name: '' };
}

function createFormFromCurrency(currency: Currency): EditorState {
  return { id: currency.id, name: currency.name };
}

export function AdminCurrenciesPage() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const [items, setItems] = useState<Currency[]>([]);
  const [loading, setLoading] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<Currency | null>(null);
  const [form, setForm] = useState<EditorState>(() => createEmptyForm());
  const [saving, setSaving] = useState(false);
  const [toast, setToast] = useState<ToastState>({ open: false, severity: 'success', message: '' });

  const showToast = useCallback((severity: ToastState['severity'], message: string) => {
    setToast({ open: true, severity, message });
  }, []);

  const loadCurrencies = useCallback(async () => {
    setLoading(true);
    try {
      const data = await api.listCurrencies();
      setItems(data);
    } catch (error) {
      showToast('error', getApiErrorMessage(error, 'Не удалось загрузить валюты.'));
    } finally {
      setLoading(false);
    }
  }, [showToast]);

  useEffect(() => {
    void loadCurrencies();
  }, [loadCurrencies]);

  const handleCreate = () => {
    setEditing(null);
    setForm(createEmptyForm());
    setDialogOpen(true);
  };

  const handleEdit = (currency: Currency) => {
    setEditing(currency);
    setForm(createFormFromCurrency(currency));
    setDialogOpen(true);
  };

  const handleCloseDialog = () => {
    if (saving) return;
    setDialogOpen(false);
    setEditing(null);
  };

  const handleSubmit = async () => {
    setSaving(true);
    try {
      if (editing) {
        const model: UpdateCurrencyModel = { name: form.name.trim() };
        await api.updateCurrency(editing.id, model);
        showToast('success', 'Валюта обновлена.');
      } else {
        const model: CurrencyModel = { id: form.id.trim().toUpperCase(), name: form.name.trim() };
        await api.createCurrency(model);
        showToast('success', 'Валюта создана.');
      }

      setDialogOpen(false);
      setEditing(null);
      await loadCurrencies();
    } catch (error) {
      showToast('error', getApiErrorMessage(error, editing ? 'Не удалось обновить валюту.' : 'Не удалось создать валюту.'));
    } finally {
      setSaving(false);
    }
  };

  const canSubmit =
    form.name.trim().length > 0 &&
    (editing != null || /^[A-Za-z]{3}$/.test(form.id.trim())) &&
    !saving;

  return (
    <Stack spacing={2}>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ alignItems: { sm: 'center' }, justifyContent: 'space-between' }}>
        <Typography variant="body2" color="text.secondary">
          Всего валют: {items.length}
        </Typography>
        <Stack direction="row" spacing={1}>
          <Tooltip title="Обновить">
            <span>
              <IconButton onClick={() => void loadCurrencies()} disabled={loading}>
                <RefreshIcon />
              </IconButton>
            </span>
          </Tooltip>
          <Button variant="contained" startIcon={<AddIcon />} onClick={handleCreate} sx={{ textTransform: 'none' }}>
            Новая валюта
          </Button>
        </Stack>
      </Stack>

      <Paper variant="outlined" sx={{ backgroundImage: 'none' }}>
        {loading && !items.length ? (
          <Stack sx={{ py: 6, alignItems: 'center' }}>
            <CircularProgress />
          </Stack>
        ) : (
          <TableContainer sx={{ maxHeight: isMobile ? 'calc(100vh - 280px)' : 'calc(100vh - 320px)' }}>
            <Table stickyHeader size={isMobile ? 'small' : 'medium'}>
              <TableHead>
                <TableRow>
                  <TableCell>Код</TableCell>
                  <TableCell>Название</TableCell>
                  <TableCell align="right" width={72} />
                </TableRow>
              </TableHead>
              <TableBody>
                {items.map((currency) => (
                  <TableRow key={currency.id} hover>
                    <TableCell sx={{ fontWeight: 600 }}>{currency.id}</TableCell>
                    <TableCell>{currency.name}</TableCell>
                    <TableCell align="right">
                      <Tooltip title="Редактировать">
                        <IconButton size="small" onClick={() => handleEdit(currency)}>
                          <EditIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    </TableCell>
                  </TableRow>
                ))}
                {!items.length && !loading && (
                  <TableRow>
                    <TableCell colSpan={3}>
                      <Box sx={{ py: 3, textAlign: 'center' }}>
                        <Typography color="text.secondary">Валюты не найдены.</Typography>
                      </Box>
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </Paper>

      <Dialog open={dialogOpen} onClose={handleCloseDialog} fullWidth maxWidth="sm" fullScreen={isMobile}>
        <DialogTitle>{editing ? 'Редактирование валюты' : 'Новая валюта'}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="Код"
              value={form.id}
              onChange={(event) => setForm((prev) => ({ ...prev, id: event.target.value.toUpperCase() }))}
              disabled={Boolean(editing) || saving}
              slotProps={{ htmlInput: { maxLength: 3 } }}
              helperText={editing ? 'Код валюты нельзя изменить.' : 'Трёхбуквенный код, например USD'}
              autoFocus={!editing}
              fullWidth
            />
            <TextField
              label="Название"
              value={form.name}
              onChange={(event) => setForm((prev) => ({ ...prev, name: event.target.value }))}
              disabled={saving}
              autoFocus={Boolean(editing)}
              fullWidth
            />
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={handleCloseDialog} disabled={saving}>
            Отмена
          </Button>
          <Button variant="contained" onClick={() => void handleSubmit()} disabled={!canSubmit}>
            {saving ? 'Сохраняем…' : editing ? 'Сохранить' : 'Создать'}
          </Button>
        </DialogActions>
      </Dialog>

      <Snackbar
        open={toast.open}
        autoHideDuration={5000}
        onClose={() => setToast((prev) => ({ ...prev, open: false }))}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert severity={toast.severity} onClose={() => setToast((prev) => ({ ...prev, open: false }))} sx={{ width: '100%' }}>
          {toast.message}
        </Alert>
      </Snackbar>
    </Stack>
  );
}
