import { useCallback, useEffect, useState } from 'react';
import {
  Box,
  Button,
  CircularProgress,
  Container,
  Divider,
  Drawer,
  Grid,
  MenuItem,
  Paper,
  Select,
  Stack,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import AddCircleOutlineIcon from '@mui/icons-material/AddCircleOutline';
import MenuIcon from '@mui/icons-material/Menu';
import { api } from './api';
import {
  Broker,
  ClearPortfolioDataResult,
  ImportResult,
  InstrumentLookup,
  Operation,
  OperationModel,
  Portfolio,
  PortfolioPerformance,
  PortfoliosSummary,
  PortfolioSummary,
} from './types';
import { SummaryCards } from './SummaryCards';
import { PositionsTable } from './PositionsTable';
import { PerformanceTable } from './PerformanceTable';
import { PortfolioSidebar } from './PortfolioSidebar';
import { QuickDeposit } from './QuickDeposit';
import { OperationsPanel } from './OperationsPanel';
import { CreatePortfolioDialog } from './CreatePortfolioDialog';

const VALUATION_METHODS = [
  { value: 'adjustingAvg', label: 'Adjusting Avg' },
  { value: 'staticAvg', label: 'Static Avg' },
  { value: 'fifo', label: 'FIFO' },
  { value: 'lifo', label: 'LIFO' },
];

interface Props {
  onLogout: () => void;
  route: 'overview' | 'operations';
  onRouteChange: (route: 'overview' | 'operations') => void;
}

const formatMoney = (value: number) => value.toLocaleString('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

function getApiErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof Error)) return fallback;
  try {
    const payload = JSON.parse(error.message) as { message?: string };
    return payload.message || fallback;
  } catch {
    return error.message || fallback;
  }
}

export function Dashboard({ onLogout, route, onRouteChange }: Props) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const [portfolios, setPortfolios] = useState<Portfolio[]>([]);
  const [brokers, setBrokers] = useState<Broker[]>([]);
  const [selectedPortfolio, setSelectedPortfolio] = useState<string | null>(null);
  const [viewMode, setViewMode] = useState<'portfolio' | 'all'>('portfolio');
  const [summary, setSummary] = useState<PortfolioSummary | null>(null);
  const [allSummary, setAllSummary] = useState<PortfoliosSummary | null>(null);
  const [performance, setPerformance] = useState<PortfolioPerformance[]>([]);
  const [valuationMethod, setValuationMethod] = useState('adjustingAvg');
  const [loadingSummary, setLoadingSummary] = useState(false);
  const [loadingAllSummary, setLoadingAllSummary] = useState(false);
  const [allSummaryError, setAllSummaryError] = useState('');
  const [operations, setOperations] = useState<Operation[]>([]);
  const [operationsPage, setOperationsPage] = useState(1);
  const [operationsPageSize, setOperationsPageSize] = useState(25);
  const [operationsTotalCount, setOperationsTotalCount] = useState(0);
  const [loadingOps, setLoadingOps] = useState(false);
  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const [createPortfolioLoading, setCreatePortfolioLoading] = useState(false);
  const [createPortfolioError, setCreatePortfolioError] = useState('');
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const portfolioPage = route;

  useEffect(() => {
    (async () => {
      try {
        await Promise.all([loadPortfolios(), loadBrokers()]);
      } catch (error) {
        console.error(error);
      }
    })();
  }, []);

  useEffect(() => {
    if (viewMode !== 'all') return;
    loadAllSummary(valuationMethod);
  }, [viewMode, valuationMethod, portfolios, selectedPortfolio]);

  useEffect(() => {
    if (viewMode !== 'portfolio' || portfolioPage !== 'overview') return;
    if (!selectedPortfolio) return;

    loadSummary(selectedPortfolio, valuationMethod);
    loadPerformance(selectedPortfolio, valuationMethod);
  }, [selectedPortfolio, valuationMethod, viewMode, portfolioPage]);

  useEffect(() => {
    if (viewMode !== 'portfolio' || portfolioPage !== 'operations') return;
    if (!selectedPortfolio) return;

    loadOperations(selectedPortfolio, operationsPage, operationsPageSize);
  }, [selectedPortfolio, viewMode, portfolioPage, operationsPage, operationsPageSize]);

  async function loadPortfolios(preferredId?: string) {
    const data = await api.listPortfolios();
    setPortfolios(data);
    if (data.length) {
      setSelectedPortfolio((prev) => preferredId ?? prev ?? data[0].id);
      return;
    }

    setSelectedPortfolio(null);
    setSummary(null);
    setAllSummary(null);
    setPerformance([]);
    setOperations([]);
    setOperationsTotalCount(0);
    setOperationsPage(1);
    onRouteChange('overview');
    setViewMode('portfolio');
  }

  async function loadBrokers() {
    const data = await api.listBrokers();
    setBrokers(data);
  }

  async function loadSummary(id: string, method: string) {
    setLoadingSummary(true);
    try {
      const data = await api.getPortfolioSummary(id, method);
      setSummary(data);
    } finally {
      setLoadingSummary(false);
    }
  }

  async function loadAllSummary(method: string) {
    const selectedCurrency = portfolios.find((x) => x.id === selectedPortfolio)?.reportingCurrencyId;
    setLoadingAllSummary(true);
    setAllSummaryError('');
    try {
      const data = await api.getPortfoliosSummary(method, selectedCurrency);
      setAllSummary(data);
    } catch (error) {
      setAllSummary(null);
      setAllSummaryError(getApiErrorMessage(error, 'Не удалось получить общий итог по всем счетам.'));
    } finally {
      setLoadingAllSummary(false);
    }
  }

  async function loadPerformance(id: string, method: string) {
    const data = await api.getPerformance(id, method);
    setPerformance(data);
  }

  async function loadOperations(id: string, page: number, pageSize: number) {
    setLoadingOps(true);
    try {
      const data = await api.listOperations(id, { page, pageSize });
      setOperations(data.items);
      setOperationsTotalCount(data.totalCount);
      if (data.page !== operationsPage) setOperationsPage(data.page);
      if (data.pageSize !== operationsPageSize) setOperationsPageSize(data.pageSize);
    } finally {
      setLoadingOps(false);
    }
  }

  function handleOpenCreatePortfolio() {
    setCreatePortfolioError('');
    loadBrokers().catch(console.error);
    if (document.activeElement instanceof HTMLElement) {
      document.activeElement.blur();
    }
    setSidebarOpen(false);
    setCreateDialogOpen(true);
  }

  function handleCloseCreatePortfolio() {
    if (createPortfolioLoading) return;
    setCreateDialogOpen(false);
  }

  async function handleCreatePortfolio(model: { name: string; brokerId: string; reportingCurrencyId: string }) {
    setCreatePortfolioLoading(true);
    setCreatePortfolioError('');
    try {
      const createdId = await api.createPortfolio(model);
      setCreateDialogOpen(false);
      await loadPortfolios(createdId);
    } catch (error) {
      setCreatePortfolioError(getApiErrorMessage(error, 'Не удалось создать счет.'));
    } finally {
      setCreatePortfolioLoading(false);
    }
  }

  async function handleQuickDeposit({ amount, currency, note }: { amount: number; currency: string; note: string }) {
    if (!selectedPortfolio) return;
    await api.createOperation(selectedPortfolio, {
      instrumentId: undefined,
      type: 'Deposit',
      quantity: 1,
      price: amount,
      fee: 0,
      currencyId: currency,
      tradeDate: new Date().toISOString(),
      settlementDate: undefined,
      note,
    });
    await loadSummary(selectedPortfolio, valuationMethod);
    await loadPerformance(selectedPortfolio, valuationMethod);
  }

  async function handleCreateOperation(model: OperationModel) {
    if (!selectedPortfolio) return;

    await api.createOperation(selectedPortfolio, model);
    if (operationsPage !== 1) {
      setOperationsPage(1);
      return;
    }

    await loadOperations(selectedPortfolio, 1, operationsPageSize);
  }

  async function handleImportOperations(file: File): Promise<ImportResult> {
    if (!selectedPortfolio) {
      throw new Error('Сначала выберите портфель.');
    }

    const broker = brokers.find((x) => x.id === activePortfolio?.brokerId);
    if (!broker?.supportsImport || !broker.code) {
      throw new Error('Для выбранного брокера импорт пока не настроен.');
    }

    try {
      const result = await api.importOperations(selectedPortfolio, broker.code, file);

      if (operationsPage !== 1) {
        setOperationsPage(1);
      } else {
        await loadOperations(selectedPortfolio, 1, operationsPageSize);
      }

      await Promise.all([
        loadSummary(selectedPortfolio, valuationMethod),
        loadPerformance(selectedPortfolio, valuationMethod),
      ]);

      return result;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Не удалось импортировать отчет.'));
    }
  }

  async function handleUpdateOperation(id: string, model: OperationModel) {
    if (!selectedPortfolio) return;
    await api.updateOperation(selectedPortfolio, id, model);
    await loadOperations(selectedPortfolio, operationsPage, operationsPageSize);
  }

  async function handleDeleteOperation(id: string) {
    if (!selectedPortfolio) return;
    await api.deleteOperation(selectedPortfolio, id);
    await loadOperations(selectedPortfolio, operationsPage, operationsPageSize);
  }

  async function handleClearPortfolioData(): Promise<ClearPortfolioDataResult> {
    if (!selectedPortfolio) {
      throw new Error('Сначала выберите портфель.');
    }

    try {
      const result = await api.clearPortfolioData(selectedPortfolio);

      setOperations([]);
      setOperationsTotalCount(0);
      if (operationsPage !== 1) {
        setOperationsPage(1);
      } else {
        await loadOperations(selectedPortfolio, 1, operationsPageSize);
      }

      await Promise.all([
        loadSummary(selectedPortfolio, valuationMethod),
        loadPerformance(selectedPortfolio, valuationMethod),
      ]);

      return result;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Не удалось очистить данные портфеля.'));
    }
  }

  const searchInstruments = useCallback((query: string): Promise<InstrumentLookup[]> => {
    return api.searchInstruments(query);
  }, []);

  function handleSelectPortfolio(id: string) {
    setViewMode('portfolio');
    setSelectedPortfolio(id);
    setAllSummaryError('');
    setOperationsPage(1);
    setSidebarOpen(false);
  }

  function handleShowAllSummary() {
    setViewMode('all');
    onRouteChange('overview');
    setSidebarOpen(false);
  }

  function handleShowOverview() {
    if (!selectedPortfolio) return;
    setViewMode('portfolio');
    onRouteChange('overview');
    setSidebarOpen(false);
  }

  function handleShowOperations() {
    if (!selectedPortfolio) return;
    setViewMode('portfolio');
    onRouteChange('operations');
    setSidebarOpen(false);
  }

  const activePortfolio = portfolios.find((x) => x.id === selectedPortfolio) ?? null;
  const activeBroker = brokers.find((x) => x.id === activePortfolio?.brokerId) ?? null;
  const canImportOperations = Boolean(activeBroker?.supportsImport && activeBroker.code);
  const currency =
    viewMode === 'all'
      ? allSummary?.reportingCurrencyId ?? activePortfolio?.reportingCurrencyId ?? '—'
      : summary?.reportingCurrencyId ?? activePortfolio?.reportingCurrencyId ?? '—';
  const isLoadingCurrent = viewMode === 'all' ? loadingAllSummary : portfolioPage === 'overview' ? loadingSummary : false;

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', color: 'text.primary' }}>
      {!isMobile && (
        <Box sx={{ bgcolor: 'rgba(255,255,255,0.02)', borderRight: '1px solid rgba(255,255,255,0.06)' }}>
          <PortfolioSidebar
            items={portfolios}
            selectedId={selectedPortfolio}
            onSelect={handleSelectPortfolio}
            onCreate={handleOpenCreatePortfolio}
            onShowAllSummary={handleShowAllSummary}
            showAllSelected={viewMode === 'all'}
            onLogout={onLogout}
          />
        </Box>
      )}
      <Drawer
        anchor="left"
        open={isMobile && sidebarOpen}
        onClose={() => setSidebarOpen(false)}
        PaperProps={{
          sx: {
            width: 'min(86vw, 320px)',
            bgcolor: 'background.paper',
            backgroundImage: 'none',
          },
        }}
      >
        <PortfolioSidebar
          items={portfolios}
          selectedId={selectedPortfolio}
          onSelect={handleSelectPortfolio}
          onCreate={handleOpenCreatePortfolio}
          onShowAllSummary={handleShowAllSummary}
          showAllSelected={viewMode === 'all'}
          onLogout={onLogout}
          mobile
        />
      </Drawer>
      <Box sx={{ flex: 1, minWidth: 0 }}>
        <Container
          maxWidth="xl"
          sx={{
            pt: { xs: 2, md: 3 },
            pb: { xs: 'calc(env(safe-area-inset-bottom) + 16px)', md: 3 },
            px: { xs: 1.5, sm: 2.5, md: 3 },
          }}
        >
          <Paper
            variant="outlined"
            sx={{
              mb: { xs: 2, md: 2.5 },
              p: { xs: 1.5, sm: 2, md: 2.5 },
              borderRadius: { xs: 2.5, md: 3 },
              position: 'relative',
              overflow: 'hidden',
              background:
                'linear-gradient(120deg, rgba(15,118,110,0.2) 0%, rgba(14,165,233,0.1) 55%, rgba(217,119,6,0.12) 100%)',
            }}
          >
            <Box
              sx={{
                position: 'absolute',
                width: 220,
                height: 220,
                right: -70,
                top: -120,
                borderRadius: '50%',
                bgcolor: 'rgba(20,184,166,0.18)',
                filter: 'blur(2px)',
              }}
            />
            <Stack
              direction={{ xs: 'column', md: 'row' }}
              alignItems={{ xs: 'stretch', md: 'center' }}
              justifyContent="space-between"
              spacing={1.5}
              sx={{ position: 'relative', zIndex: 1 }}
            >
              <Stack spacing={0.5}>
                <Typography variant="overline" color="text.secondary">
                  {viewMode === 'all' ? 'Режим просмотра' : portfolioPage === 'operations' ? 'Операции портфеля' : 'Активный портфель'}
                </Typography>
                <Typography variant="h5" fontWeight={700} sx={{ fontSize: { xs: '1.25rem', sm: '1.5rem' } }}>
                  {viewMode === 'all' ? 'Все счета' : activePortfolio?.name ?? 'Выберите портфель'}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Валюта отчета: {currency}
                </Typography>
              </Stack>
              <Stack spacing={1} sx={{ width: { xs: '100%', md: 'auto' } }}>
                <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ width: { xs: '100%', md: 'auto' } }}>
                  {isMobile && (
                    <Button
                      variant="outlined"
                      onClick={() => setSidebarOpen(true)}
                      startIcon={<MenuIcon />}
                      sx={{ textTransform: 'none' }}
                      fullWidth
                    >
                      Портфели
                    </Button>
                  )}
                  <Button
                    variant="outlined"
                    onClick={handleOpenCreatePortfolio}
                    startIcon={<AddCircleOutlineIcon />}
                    sx={{ textTransform: 'none' }}
                    fullWidth={isMobile}
                  >
                    Новый счет
                  </Button>
                </Stack>
                {viewMode === 'portfolio' && selectedPortfolio && (
                  <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ width: { xs: '100%', md: 'auto' } }}>
                    <Button
                      variant={portfolioPage === 'overview' ? 'contained' : 'outlined'}
                      onClick={handleShowOverview}
                      sx={{ textTransform: 'none' }}
                      fullWidth={isMobile}
                    >
                      Обзор
                    </Button>
                    <Button
                      variant={portfolioPage === 'operations' ? 'contained' : 'outlined'}
                      onClick={handleShowOperations}
                      sx={{ textTransform: 'none' }}
                      fullWidth={isMobile}
                    >
                      Операции
                    </Button>
                  </Stack>
                )}
                <Stack
                  direction={{ xs: 'column', sm: 'row' }}
                  spacing={1}
                  alignItems={{ xs: 'stretch', sm: 'center' }}
                  sx={{ width: { xs: '100%', md: 'auto' } }}
                >
                  <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} alignItems={{ xs: 'stretch', sm: 'center' }}>
                    <Typography variant="overline" color="text.secondary" sx={{ lineHeight: 1.8 }}>
                      Метод оценки
                    </Typography>
                    <Select
                      size="small"
                      value={valuationMethod}
                      onChange={(e) => setValuationMethod(e.target.value)}
                      sx={{ minWidth: { xs: '100%', sm: 180 } }}
                      disabled={viewMode === 'portfolio' && portfolioPage === 'operations'}
                    >
                      {VALUATION_METHODS.map((m) => (
                        <MenuItem key={m.value} value={m.value}>
                          {m.label}
                        </MenuItem>
                      ))}
                    </Select>
                  </Stack>
                  <Paper
                    variant="outlined"
                    sx={{ px: 1.5, py: 0.75, borderRadius: 999, alignSelf: { xs: 'flex-start', sm: 'center' } }}
                  >
                    <Typography variant="caption" color="text.secondary">
                      Базовая валюта
                    </Typography>
                    <Typography variant="subtitle1" fontWeight={700} lineHeight={1.2}>
                      {currency}
                    </Typography>
                  </Paper>
                </Stack>
              </Stack>
            </Stack>
          </Paper>

          {isLoadingCurrent && (
            <Stack alignItems="center" sx={{ py: 4 }}>
              <CircularProgress />
            </Stack>
          )}

          {viewMode === 'portfolio' && portfolioPage === 'overview' && !loadingSummary && summary && (
            <Stack spacing={{ xs: 2, md: 3 }}>
              <SummaryCards summary={summary} />

              <Grid container spacing={{ xs: 2, md: 3 }}>
                <Grid item xs={12} md={8}>
                  <Stack spacing={1}>
                    <Typography variant="h6" fontWeight={700}>
                      Позиции
                    </Typography>
                    <PositionsTable positions={summary.positions} />
                  </Stack>
                </Grid>
                <Grid item xs={12} md={4}>
                  <Paper variant="outlined" sx={{ p: { xs: 1.5, sm: 2 }, height: '100%', backgroundImage: 'none' }}>
                    <QuickDeposit onSubmit={handleQuickDeposit} disabled={!selectedPortfolio} />
                  </Paper>
                </Grid>
              </Grid>

              <Stack spacing={1}>
                <Typography variant="h6" fontWeight={700}>
                  Помесячная доходность
                </Typography>
                <PerformanceTable items={performance} />
              </Stack>
            </Stack>
          )}

          {viewMode === 'portfolio' && portfolioPage === 'operations' && selectedPortfolio && (
            <Stack spacing={{ xs: 2, md: 3 }}>
              <OperationsPanel
                items={operations}
                loading={loadingOps}
                page={operationsPage}
                pageSize={operationsPageSize}
                totalCount={operationsTotalCount}
                onPageChange={(page) => setOperationsPage(page)}
                onPageSizeChange={(pageSize) => {
                  setOperationsPageSize(pageSize);
                  setOperationsPage(1);
                }}
                onCreate={handleCreateOperation}
                onImport={handleImportOperations}
                onClearPortfolioData={handleClearPortfolioData}
                canImport={canImportOperations}
                importDisabledReason="Для брокера выбранного счета импорт пока не настроен."
                onUpdate={handleUpdateOperation}
                onDelete={handleDeleteOperation}
                searchInstruments={searchInstruments}
              />
            </Stack>
          )}

          {viewMode === 'all' && !loadingAllSummary && allSummary && (
            <Stack spacing={{ xs: 2, md: 3 }}>
              <SummaryCards
                summary={{
                  id: 'all-portfolios',
                  name: 'Все счета',
                  reportingCurrencyId: allSummary.reportingCurrencyId,
                  netInflowBase: allSummary.netInflowBase,
                  cashBase: allSummary.cashBase,
                  positionsValueBase: allSummary.positionsValueBase,
                  realizedBase: allSummary.realizedBase,
                  unrealizedBase: allSummary.unrealizedBase,
                  navBase: allSummary.navBase,
                  valuationMethod: allSummary.valuationMethod,
                  cash: [],
                  positions: [],
                  realizedByInstrument: [],
                }}
              />
              <Paper variant="outlined" sx={{ p: { xs: 1.5, sm: 2 }, backgroundImage: 'none' }}>
                <Stack
                  direction={{ xs: 'column', sm: 'row' }}
                  spacing={1.5}
                  alignItems={{ xs: 'flex-start', sm: 'center' }}
                  justifyContent="space-between"
                >
                  <Box>
                    <Typography variant="body2" color="text.secondary">
                      Итого по всем счетам ({allSummary.portfolioCount})
                    </Typography>
                    <Typography variant="h6" fontWeight={700}>
                      Прибыль / убыток
                    </Typography>
                  </Box>
                  <Typography
                    variant="h5"
                    fontWeight={800}
                    color={allSummary.pnlBase >= 0 ? 'success.main' : 'error.main'}
                    sx={{ whiteSpace: 'nowrap' }}
                  >
                    {formatMoney(allSummary.pnlBase)} {allSummary.reportingCurrencyId}
                  </Typography>
                </Stack>
                <Divider sx={{ my: 1.5 }} />
                <Typography variant="body2" color="text.secondary">
                  Чтобы вернуться к деталям по одному счету, выберите нужный счет в списке слева.
                </Typography>
              </Paper>
            </Stack>
          )}

          {viewMode === 'all' && !loadingAllSummary && !allSummary && allSummaryError && (
            <Paper variant="outlined" sx={{ p: { xs: 2, md: 3 }, textAlign: 'center', backgroundImage: 'none' }}>
              <Typography color="error.main">{allSummaryError}</Typography>
            </Paper>
          )}

          {viewMode === 'portfolio' && !selectedPortfolio && (
            <Paper variant="outlined" sx={{ p: { xs: 2, md: 3 }, textAlign: 'center', backgroundImage: 'none' }}>
              <Typography color="text.secondary">Выберите портфель или создайте новый</Typography>
            </Paper>
          )}

          {viewMode === 'portfolio' && portfolioPage === 'overview' && !loadingSummary && !summary && selectedPortfolio && (
            <Paper variant="outlined" sx={{ p: { xs: 2, md: 3 }, textAlign: 'center', backgroundImage: 'none' }}>
              <Typography color="text.secondary">Не удалось загрузить обзор портфеля</Typography>
            </Paper>
          )}
        </Container>
      </Box>
      <CreatePortfolioDialog
        open={createDialogOpen}
        brokers={brokers}
        submitting={createPortfolioLoading}
        error={createPortfolioError}
        onClose={handleCloseCreatePortfolio}
        onSubmit={handleCreatePortfolio}
      />
    </Box>
  );
}
