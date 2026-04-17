import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  IconButton,
  Pagination,
  Paper,
  Snackbar,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
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
import { InstrumentCorporateActionsDialog } from './InstrumentCorporateActionsDialog';
import { InstrumentEditorDialog } from './InstrumentEditorDialog';
import { Category, Currency, Instrument, InstrumentCorporateAction, InstrumentCorporateActionModel, InstrumentModel } from './types';

const TYPE_LABELS: Record<Instrument['type'], string> = {
  Equity: 'Акция',
  Bond: 'Облигация',
  Etf: 'ETF',
  Currency: 'Валюта',
  Commodity: 'Товар',
  Crypto: 'Крипто',
};

interface ToastState {
  open: boolean;
  severity: 'success' | 'error';
  message: string;
}

const PRICE_SOURCE_LABELS: Record<'MOEX' | 'TBANK', string> = {
  MOEX: 'MOEX',
  TBANK: 'T-Bank',
};

export function AdminInstrumentsPage() {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const [categories, setCategories] = useState<Category[]>([]);
  const [categoriesLoading, setCategoriesLoading] = useState(false);
  const [currencies, setCurrencies] = useState<Currency[]>([]);
  const [currenciesLoading, setCurrenciesLoading] = useState(false);
  const [items, setItems] = useState<Instrument[]>([]);
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [totalCount, setTotalCount] = useState(0);
  const [searchInput, setSearchInput] = useState('');
  const [countryInput, setCountryInput] = useState('');
  const [query, setQuery] = useState('');
  const [country, setCountry] = useState('');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<Instrument | null>(null);
  const [loadingEditor, setLoadingEditor] = useState(false);
  const [saving, setSaving] = useState(false);
  const [actionsDialogOpen, setActionsDialogOpen] = useState(false);
  const [actionsInstrument, setActionsInstrument] = useState<Instrument | null>(null);
  const [corporateActions, setCorporateActions] = useState<InstrumentCorporateAction[]>([]);
  const [actionsLoading, setActionsLoading] = useState(false);
  const [actionsSaving, setActionsSaving] = useState(false);
  const [toast, setToast] = useState<ToastState>({ open: false, severity: 'success', message: '' });

  const showToast = useCallback((severity: ToastState['severity'], message: string) => {
    setToast({ open: true, severity, message });
  }, []);

  const categoryMap = useMemo(() => {
    return new Map(categories.map((category) => [category.id, category.name]));
  }, [categories]);

  const totalPages = useMemo(() => {
    return pageSize > 0 ? Math.ceil(totalCount / pageSize) : 0;
  }, [pageSize, totalCount]);

  const loadCategories = useCallback(async () => {
    setCategoriesLoading(true);
    try {
      const data = await api.listCategories();
      setCategories(data);
    } catch (error) {
      showToast('error', getApiErrorMessage(error, 'Не удалось загрузить категории.'));
    } finally {
      setCategoriesLoading(false);
    }
  }, [showToast]);

  const loadCurrencies = useCallback(async () => {
    setCurrenciesLoading(true);
    try {
      const data = await api.listCurrencies();
      setCurrencies(data);
    } catch (error) {
      showToast('error', getApiErrorMessage(error, 'Не удалось загрузить валюты.'));
    } finally {
      setCurrenciesLoading(false);
    }
  }, [showToast]);

  const loadInstruments = useCallback(async () => {
    setLoading(true);
    try {
      const data = await api.listAdminInstruments({ query, country, page, pageSize });
      setItems(data.items);
      setTotalCount(data.totalCount);
      if (data.page !== page) {
        setPage(data.page);
      }
      if (data.pageSize !== pageSize) {
        setPageSize(data.pageSize);
      }
    } catch (error) {
      setItems([]);
      setTotalCount(0);
      showToast('error', getApiErrorMessage(error, 'Не удалось загрузить список инструментов.'));
    } finally {
      setLoading(false);
    }
  }, [country, page, pageSize, query, showToast]);

  useEffect(() => {
    void loadCategories();
  }, [loadCategories]);

  useEffect(() => {
    void loadCurrencies();
  }, [loadCurrencies]);

  useEffect(() => {
    const timerId = window.setTimeout(() => {
      setPage(1);
      setQuery(searchInput.trim());
      setCountry(countryInput.trim());
    }, 300);

    return () => window.clearTimeout(timerId);
  }, [countryInput, searchInput]);

  useEffect(() => {
    void loadInstruments();
  }, [loadInstruments]);

  const openCreateDialog = () => {
    setEditing(null);
    setDialogOpen(true);
  };

  const handleEdit = async (id: string) => {
    setLoadingEditor(true);
    try {
      const instrument = await api.getInstrument(id);
      setEditing(instrument);
      setDialogOpen(true);
    } catch (error) {
      showToast('error', getApiErrorMessage(error, 'Не удалось загрузить инструмент.'));
    } finally {
      setLoadingEditor(false);
    }
  };

  const handleSubmit = async (model: InstrumentModel) => {
    setSaving(true);
    try {
      if (editing) {
        await api.updateInstrument(editing.id, model);
        showToast('success', 'Инструмент обновлен.');
      } else {
        await api.createInstrument(model);
        showToast('success', 'Инструмент создан.');
      }

      setDialogOpen(false);
      setEditing(null);
      await loadInstruments();
    } catch (error) {
      showToast('error', getApiErrorMessage(error, editing ? 'Не удалось обновить инструмент.' : 'Не удалось создать инструмент.'));
    } finally {
      setSaving(false);
    }
  };

  const openCorporateActions = async (instrument: Instrument) => {
    setActionsInstrument(instrument);
    setActionsDialogOpen(true);
    setActionsLoading(true);
    try {
      const data = await api.listInstrumentCorporateActions(instrument.id);
      setCorporateActions(data);
    } catch (error) {
      setCorporateActions([]);
      showToast('error', getApiErrorMessage(error, 'Не удалось загрузить корпоративные действия.'));
    } finally {
      setActionsLoading(false);
    }
  };

  const reloadCorporateActions = useCallback(async () => {
    if (!actionsInstrument) {
      return;
    }

    const data = await api.listInstrumentCorporateActions(actionsInstrument.id);
    setCorporateActions(data);
  }, [actionsInstrument]);

  const handleCreateCorporateAction = async (model: InstrumentCorporateActionModel) => {
    if (!actionsInstrument) {
      return;
    }

    setActionsSaving(true);
    try {
      await api.createInstrumentCorporateAction(actionsInstrument.id, model);
      await reloadCorporateActions();
      showToast('success', 'Корпоративное действие добавлено.');
    } catch (error) {
      showToast('error', getApiErrorMessage(error, 'Не удалось добавить корпоративное действие.'));
      throw error;
    } finally {
      setActionsSaving(false);
    }
  };

  const handleUpdateCorporateAction = async (id: string, model: InstrumentCorporateActionModel) => {
    if (!actionsInstrument) {
      return;
    }

    setActionsSaving(true);
    try {
      await api.updateInstrumentCorporateAction(actionsInstrument.id, id, model);
      await reloadCorporateActions();
      showToast('success', 'Корпоративное действие обновлено.');
    } catch (error) {
      showToast('error', getApiErrorMessage(error, 'Не удалось обновить корпоративное действие.'));
      throw error;
    } finally {
      setActionsSaving(false);
    }
  };

  const handleDeleteCorporateAction = async (id: string) => {
    if (!actionsInstrument) {
      return;
    }

    const confirmed = window.confirm('Удалить корпоративное действие? Портфели с этой бумагой будут пересчитаны.');
    if (!confirmed) {
      return;
    }

    setActionsSaving(true);
    try {
      await api.deleteInstrumentCorporateAction(actionsInstrument.id, id);
      await reloadCorporateActions();
      showToast('success', 'Корпоративное действие удалено.');
    } catch (error) {
      showToast('error', getApiErrorMessage(error, 'Не удалось удалить корпоративное действие.'));
    } finally {
      setActionsSaving(false);
    }
  };

  return (
    <>
      <Paper variant="outlined" sx={{ p: { xs: 1.5, sm: 2 }, backgroundImage: 'none' }}>
        <Stack spacing={2}>
          <Stack
            direction={{ xs: 'column', md: 'row' }}
            spacing={1.5}
            sx={{ justifyContent: 'space-between', alignItems: { xs: 'stretch', md: 'center' } }}
          >
            <Stack spacing={0.5}>
              <Typography variant="h6" sx={{ fontWeight: 700 }}>
                Справочник инструментов
              </Typography>
              <Typography variant="body2" color="text.secondary">
                Поиск, добавление и редактирование инструментов доступны только администратору.
              </Typography>
            </Stack>
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
              <Button
                variant="outlined"
                startIcon={<RefreshIcon />}
                onClick={() => void Promise.all([loadCategories(), loadCurrencies(), loadInstruments()])}
                disabled={loading || categoriesLoading || currenciesLoading || loadingEditor || saving}
                sx={{ textTransform: 'none' }}
              >
                Обновить
              </Button>
              <Button
                variant="contained"
                startIcon={<AddIcon />}
                onClick={openCreateDialog}
                disabled={categories.length === 0 || currencies.length === 0 || categoriesLoading || currenciesLoading || loadingEditor || saving}
                sx={{ textTransform: 'none' }}
              >
                Новый инструмент
              </Button>
            </Stack>
          </Stack>

          <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5}>
            <TextField
              label="Поиск по тикеру, названию, ISIN, FIGI или бирже"
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              fullWidth
            />
            <TextField
              label="Фильтр по стране"
              value={countryInput}
              onChange={(e) => setCountryInput(e.target.value)}
              fullWidth
              sx={{ maxWidth: { md: 280 } }}
            />
          </Stack>

          {loading || categoriesLoading || currenciesLoading || loadingEditor ? (
            <Stack sx={{ py: 4, alignItems: 'center' }}>
              <CircularProgress />
            </Stack>
          ) : isMobile ? (
            <Stack spacing={1.25}>
              {items.map((item) => (
                <Paper key={item.id} variant="outlined" sx={{ p: 1.5, backgroundImage: 'none' }}>
                  <Stack spacing={1.25}>
                    <Stack direction="row" spacing={1} sx={{ justifyContent: 'space-between', alignItems: 'flex-start' }}>
                      <Stack spacing={0.25}>
                        <Typography sx={{ fontWeight: 700 }}>{item.ticker}</Typography>
                        <Typography variant="body2">{item.name}</Typography>
                      </Stack>
                      <Stack direction="row" spacing={1}>
                        <Button variant="outlined" size="small" onClick={() => void openCorporateActions(item)}>
                          Сплиты
                        </Button>
                        <Tooltip title="Редактировать">
                          <IconButton size="small" onClick={() => void handleEdit(item.id)}>
                            <EditIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      </Stack>
                    </Stack>
                    <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap' }}>
                      <Chip size="small" label={TYPE_LABELS[item.type]} />
                      <Chip size="small" label={item.currencyId} variant="outlined" />
                      <Chip size="small" label={item.priceSource ? PRICE_SOURCE_LABELS[item.priceSource] : 'Без синхронизации'} variant="outlined" />
                      <Chip size="small" label={item.isTrading ? 'Торгуется' : 'Не торгуется'} color={item.isTrading ? 'success' : 'default'} />
                    </Stack>
                    <Typography variant="body2" color="text.secondary">
                      ISIN: {item.isin}
                    </Typography>
                    <Typography variant="body2" color="text.secondary">
                      Категория: {categoryMap.get(item.categoryId) ?? `#${item.categoryId}`}
                    </Typography>
                    <Typography variant="body2" color="text.secondary">
                      Биржа: {item.exchange ?? '—'} | Страна: {item.country ?? '—'}
                    </Typography>
                  </Stack>
                </Paper>
              ))}
              {!items.length && (
                <Paper variant="outlined" sx={{ p: 2, textAlign: 'center', backgroundImage: 'none' }}>
                  <Typography color="text.secondary">Инструменты не найдены</Typography>
                </Paper>
              )}
            </Stack>
          ) : (
            <TableContainer component={Box} sx={{ borderRadius: 2, overflowX: 'auto' }}>
              <Table size="small" stickyHeader>
                <TableHead>
                  <TableRow>
                    <TableCell>Тикер</TableCell>
                    <TableCell>Название</TableCell>
                    <TableCell>ISIN</TableCell>
                    <TableCell>Тип</TableCell>
                    <TableCell>Валюта</TableCell>
                    <TableCell>Категория</TableCell>
                    <TableCell>Биржа</TableCell>
                    <TableCell>Страна</TableCell>
                    <TableCell>Источник цен</TableCell>
                    <TableCell>Статус</TableCell>
                    <TableCell align="right">Действия</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {items.map((item) => (
                    <TableRow key={item.id} hover>
                      <TableCell>{item.ticker}</TableCell>
                      <TableCell>{item.name}</TableCell>
                      <TableCell>{item.isin}</TableCell>
                      <TableCell>{TYPE_LABELS[item.type]}</TableCell>
                      <TableCell>{item.currencyId}</TableCell>
                      <TableCell>{categoryMap.get(item.categoryId) ?? `#${item.categoryId}`}</TableCell>
                      <TableCell>{item.exchange ?? '—'}</TableCell>
                      <TableCell>{item.country ?? '—'}</TableCell>
                      <TableCell>{item.priceSource ? PRICE_SOURCE_LABELS[item.priceSource] : '—'}</TableCell>
                      <TableCell>
                        <Chip
                          size="small"
                          label={item.isTrading ? 'Торгуется' : 'Не торгуется'}
                          color={item.isTrading ? 'success' : 'default'}
                        />
                      </TableCell>
                      <TableCell align="right">
                        <Stack direction="row" spacing={1} sx={{ justifyContent: 'flex-end' }}>
                          <Button variant="outlined" size="small" onClick={() => void openCorporateActions(item)}>
                            Сплиты
                          </Button>
                          <Tooltip title="Редактировать">
                            <IconButton size="small" onClick={() => void handleEdit(item.id)}>
                              <EditIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        </Stack>
                      </TableCell>
                    </TableRow>
                  ))}
                  {!items.length && (
                    <TableRow>
                      <TableCell colSpan={11} align="center">
                        <Typography color="text.secondary" sx={{ py: 2 }}>
                          Инструменты не найдены
                        </Typography>
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </TableContainer>
          )}

          <Stack spacing={1.5}>
            {totalPages > 1 && (
              <Stack
                direction={{ xs: 'column', sm: 'row' }}
                spacing={1}
                sx={{ alignItems: 'center', justifyContent: 'space-between' }}
              >
                <Typography variant="body2" color="text.secondary">
                  Страница {page} из {totalPages}
                </Typography>
                <Pagination
                  count={totalPages}
                  page={page}
                  onChange={(_, nextPage) => setPage(nextPage)}
                  color="primary"
                  shape="rounded"
                  showFirstButton
                  showLastButton
                  size={isMobile ? 'small' : 'medium'}
                  siblingCount={isMobile ? 0 : 1}
                  boundaryCount={isMobile ? 1 : 2}
                />
              </Stack>
            )}

            <TablePagination
              component="div"
              count={totalCount}
              page={Math.max(page - 1, 0)}
              onPageChange={(_, nextPage) => setPage(nextPage + 1)}
              rowsPerPage={pageSize}
              onRowsPerPageChange={(e) => {
                setPageSize(Number(e.target.value));
                setPage(1);
              }}
              rowsPerPageOptions={[10, 25, 50, 100]}
              labelRowsPerPage="Строк на странице"
              showFirstButton
              showLastButton
            />
          </Stack>
        </Stack>
      </Paper>

      <InstrumentEditorDialog
        key={`${editing?.id ?? 'new'}:${categories.map((category) => category.id).join(',')}:${currencies.map((currency) => currency.id).join(',')}`}
        open={dialogOpen}
        initial={editing}
        categories={categories}
        currencies={currencies}
        submitting={saving}
        onClose={() => {
          if (saving) {
            return;
          }

          setDialogOpen(false);
          setEditing(null);
        }}
        onSubmit={handleSubmit}
      />

      <InstrumentCorporateActionsDialog
        open={actionsDialogOpen}
        instrument={actionsInstrument}
        items={corporateActions}
        loading={actionsLoading}
        submitting={actionsSaving}
        onClose={() => {
          if (actionsSaving) {
            return;
          }

          setActionsDialogOpen(false);
          setActionsInstrument(null);
          setCorporateActions([]);
        }}
        onCreate={handleCreateCorporateAction}
        onUpdate={handleUpdateCorporateAction}
        onDelete={handleDeleteCorporateAction}
      />

      <Snackbar
        open={toast.open}
        autoHideDuration={4000}
        onClose={() => setToast((prev) => ({ ...prev, open: false }))}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert
          onClose={() => setToast((prev) => ({ ...prev, open: false }))}
          severity={toast.severity}
          sx={{ width: '100%' }}
        >
          {toast.message}
        </Alert>
      </Snackbar>
    </>
  );
}
