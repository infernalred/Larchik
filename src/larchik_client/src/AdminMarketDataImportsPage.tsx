import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Chip,
  CircularProgress,
  Divider,
  LinearProgress,
  MenuItem,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import CloudDownloadOutlinedIcon from '@mui/icons-material/CloudDownloadOutlined';
import RefreshIcon from '@mui/icons-material/Refresh';
import { api } from './api';
import { getApiErrorMessage } from './error-utils';
import {
  createMarketDataImportForm,
  isTerminalMarketDataImportStatus,
  MARKET_DATA_IMPORT_STATUS_LABELS,
  normalizeMarketDataImportForm,
  validateMarketDataImportForm,
} from './market-data-import-domain';
import { MarketDataImportModel, MarketDataImportRequest, MarketDataImportStatus } from './types';

const POLL_INTERVAL_MS = 2_000;

const STATUS_COLORS: Record<MarketDataImportStatus, 'default' | 'info' | 'success' | 'warning' | 'error'> = {
  Queued: 'default',
  ResolvingInstrument: 'info',
  LoadingPrices: 'info',
  Succeeded: 'success',
  SkippedExisting: 'warning',
  Failed: 'error',
};

function utcToday(): string {
  return new Date().toISOString().slice(0, 10);
}

export function AdminMarketDataImportsPage() {
  const today = useMemo(utcToday, []);
  const [form, setForm] = useState<MarketDataImportModel>(() => createMarketDataImportForm(today));
  const [request, setRequest] = useState<MarketDataImportRequest | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [statusError, setStatusError] = useState<string | null>(null);
  const requestId = request?.id;
  const requestStatus = request?.status;

  const refreshStatus = useCallback(async (id: string, showProgress = false) => {
    if (showProgress) setRefreshing(true);
    try {
      const current = await api.getMarketDataImport(id);
      setRequest(current);
      setStatusError(null);
      return current;
    } catch (error) {
      setStatusError(getApiErrorMessage(error, 'Не удалось обновить статус заявки.'));
      return null;
    } finally {
      if (showProgress) setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    if (!requestId || !requestStatus || isTerminalMarketDataImportStatus(requestStatus)) {
      return;
    }

    let cancelled = false;
    let timeoutId: number | undefined;
    const poll = async () => {
      const current = await refreshStatus(requestId);
      if (!cancelled && (!current || !isTerminalMarketDataImportStatus(current.status))) {
        timeoutId = window.setTimeout(() => void poll(), POLL_INTERVAL_MS);
      }
    };

    timeoutId = window.setTimeout(() => void poll(), POLL_INTERVAL_MS);
    return () => {
      cancelled = true;
      if (timeoutId != null) window.clearTimeout(timeoutId);
    };
  }, [refreshStatus, requestId, requestStatus]);

  const updateForm = <K extends keyof MarketDataImportModel>(key: K, value: MarketDataImportModel[K]) => {
    setForm((previous) => ({ ...previous, [key]: value }));
    setFormError(null);
  };

  const handleSubmit = async () => {
    const error = validateMarketDataImportForm(form, today);
    if (error) {
      setFormError(error);
      return;
    }

    setSubmitting(true);
    setStatusError(null);
    try {
      const queued = await api.queueMarketDataImport(normalizeMarketDataImportForm(form), window.crypto.randomUUID());
      setRequest(queued);
    } catch (submitError) {
      setFormError(getApiErrorMessage(submitError, 'Не удалось создать заявку на импорт.'));
    } finally {
      setSubmitting(false);
    }
  };

  const isProcessing = request != null && !isTerminalMarketDataImportStatus(request.status);

  return (
    <Stack spacing={2.5}>
      <Paper variant="outlined" sx={{ p: { xs: 2, md: 2.5 }, backgroundImage: 'none' }}>
        <Stack spacing={2}>
          <Stack spacing={0.5}>
            <Typography variant="h6">Новый инструмент и история цен</Typography>
            <Typography variant="body2" color="text.secondary">
              Укажите источник, ISIN и дату начала. Если ISIN уже есть в справочнике, внешний источник вызван не будет.
            </Typography>
          </Stack>

          {formError && <Alert severity="error">{formError}</Alert>}

          <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
            <TextField
              select
              label="Источник"
              value={form.source}
              onChange={(event) => updateForm('source', event.target.value as MarketDataImportModel['source'])}
              disabled={submitting}
              sx={{ minWidth: { md: 180 } }}
            >
              <MenuItem value="MOEX">Московская биржа</MenuItem>
              <MenuItem value="TBANK">T‑Bank Invest API</MenuItem>
            </TextField>
            <TextField
              label="ISIN"
              value={form.isin}
              onChange={(event) => updateForm('isin', event.target.value.toUpperCase())}
              disabled={submitting}
              placeholder="RU000A107T19"
              slotProps={{ htmlInput: { maxLength: 12 } }}
              sx={{ flex: 1 }}
            />
            <TextField
              label="Загрузить цены с"
              type="date"
              value={form.fromDate}
              onChange={(event) => updateForm('fromDate', event.target.value)}
              disabled={submitting}
              slotProps={{ htmlInput: { max: today }, inputLabel: { shrink: true } }}
              sx={{ minWidth: { md: 220 } }}
            />
          </Stack>

          <Button
            variant="contained"
            startIcon={submitting ? <CircularProgress size={18} color="inherit" /> : <CloudDownloadOutlinedIcon />}
            onClick={() => void handleSubmit()}
            disabled={submitting}
            sx={{ alignSelf: { xs: 'stretch', sm: 'flex-start' } }}
          >
            {submitting ? 'Отправляем…' : 'Поставить в очередь'}
          </Button>
        </Stack>
      </Paper>

      {request && (
        <Paper variant="outlined" sx={{ p: { xs: 2, md: 2.5 }, backgroundImage: 'none' }}>
          <Stack spacing={2}>
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ justifyContent: 'space-between', alignItems: { sm: 'center' } }}>
              <Stack spacing={0.5}>
                <Typography variant="h6">Статус заявки</Typography>
                <Typography variant="caption" color="text.secondary" sx={{ wordBreak: 'break-all' }}>
                  {request.id}
                </Typography>
              </Stack>
              <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                <Chip label={MARKET_DATA_IMPORT_STATUS_LABELS[request.status]} color={STATUS_COLORS[request.status]} />
                <Button
                  variant="outlined"
                  size="small"
                  startIcon={refreshing ? <CircularProgress size={16} /> : <RefreshIcon />}
                  onClick={() => void refreshStatus(request.id, true)}
                  disabled={refreshing}
                >
                  Обновить
                </Button>
              </Stack>
            </Stack>

            {isProcessing && <LinearProgress />}
            {statusError && <Alert severity="warning">{statusError}</Alert>}
            {request.status === 'SkippedExisting' && (
              <Alert severity="info">Инструмент с ISIN {request.isin} уже существует. Запросы в RabbitMQ и источник не отправлялись.</Alert>
            )}
            {request.status === 'Succeeded' && (
              <Alert severity="success">Инструмент и история цен успешно обработаны.</Alert>
            )}
            {request.status === 'Failed' && (
              <Alert severity="error">{request.lastError || 'Импорт завершился с ошибкой.'}</Alert>
            )}

            <Divider />
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={{ xs: 1, sm: 4 }}>
              <Stack>
                <Typography variant="caption" color="text.secondary">Источник</Typography>
                <Typography>{request.source}</Typography>
              </Stack>
              <Stack>
                <Typography variant="caption" color="text.secondary">ISIN</Typography>
                <Typography>{request.isin}</Typography>
              </Stack>
              <Stack>
                <Typography variant="caption" color="text.secondary">Период</Typography>
                <Typography>{request.fromDate} — {request.toDate}</Typography>
              </Stack>
              <Stack>
                <Typography variant="caption" color="text.secondary">Цены</Typography>
                <Typography>Добавлено: {request.insertedPrices}, обновлено: {request.updatedPrices}</Typography>
              </Stack>
            </Stack>
          </Stack>
        </Paper>
      )}
    </Stack>
  );
}
