import { lazy, Suspense, useCallback, useEffect, useRef, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Container,
  Drawer,
  Grid,
  MenuItem,
  Paper,
  Select,
  Snackbar,
  Stack,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import AddCircleOutlinedIcon from '@mui/icons-material/AddCircleOutlined';
import MenuIcon from '@mui/icons-material/Menu';
import { api } from './api';
import { getApiErrorMessage } from './error-utils';
import {
  Broker,
  ClearPortfolioDataResult,
  Currency,
  ImportResult,
  InstrumentLookup,
  Operation,
  OperationModel,
  Portfolio,
  PortfolioPerformance,
  PortfolioSummary,
  RecalculatePortfolioResult,
  User,
} from './types';
import { SummaryCards } from './SummaryCards';
import { PositionsTable } from './PositionsTable';
import { buildDisplayPositions } from './portfolio-summary-domain';
import { PerformanceAnalytics } from './PerformanceAnalytics';
import { PortfolioSidebar } from './PortfolioSidebar';
import { QuickDeposit } from './QuickDeposit';
import { OperationsPanel } from './OperationsPanel';
import { CreatePortfolioDialog } from './CreatePortfolioDialog';
import { ChangePasswordDialog } from './ChangePasswordDialog';

type PortfolioRoute = 'overview' | 'operations' | 'analytics' | 'instruments' | 'currencies';

function isAdminReferencePage(route: PortfolioRoute): boolean {
  return route === 'instruments' || route === 'currencies';
}

const VALUATION_METHODS = [
  { value: 'adjustingAvg', label: 'Adjusting Avg' },
  { value: 'staticAvg', label: 'Static Avg' },
  { value: 'fifo', label: 'FIFO' },
  { value: 'lifo', label: 'LIFO' },
];

interface Props {
  onLogout: () => void;
  route: PortfolioRoute;
  onRouteChange: (route: PortfolioRoute) => void;
  user: User;
}

const AdminInstrumentsPage = lazy(async () => {
  const module = await import('./AdminInstrumentsPage');
  return { default: module.AdminInstrumentsPage };
});
const AdminCurrenciesPage = lazy(async () => {
  const module = await import('./AdminCurrenciesPage');
  return { default: module.AdminCurrenciesPage };
});

function formatPercent(value: number): string {
  return `${(value * 100).toLocaleString('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}%`;
}

export function Dashboard({ onLogout, route, onRouteChange, user }: Props) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const [portfolios, setPortfolios] = useState<Portfolio[]>([]);
  const [brokers, setBrokers] = useState<Broker[]>([]);
  const [currencies, setCurrencies] = useState<Currency[]>([]);
  const [selectedPortfolio, setSelectedPortfolio] = useState<string | null>(null);
  const [viewMode, setViewMode] = useState<'portfolio' | 'all'>('portfolio');
  const [summary, setSummary] = useState<PortfolioSummary | null>(null);
  const [aggregateSummary, setAggregateSummary] = useState<PortfolioSummary | null>(null);
  const [performance, setPerformance] = useState<PortfolioPerformance[]>([]);
  const [aggregatePerformance, setAggregatePerformance] = useState<PortfolioPerformance[]>([]);
  const [valuationMethod, setValuationMethod] = useState('adjustingAvg');
  const [loadingSummary, setLoadingSummary] = useState(false);
  const [loadingPerformance, setLoadingPerformance] = useState(false);
  const [loadingAggregateSummary, setLoadingAggregateSummary] = useState(false);
  const [loadingAggregatePerformance, setLoadingAggregatePerformance] = useState(false);
  const [aggregateError, setAggregateError] = useState('');
  const [operations, setOperations] = useState<Operation[]>([]);
  const [operationsPage, setOperationsPage] = useState(1);
  const [operationsPageSize, setOperationsPageSize] = useState(25);
  const [operationsTotalCount, setOperationsTotalCount] = useState(0);
  const [loadingOps, setLoadingOps] = useState(false);
  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const [createPortfolioLoading, setCreatePortfolioLoading] = useState(false);
  const [createPortfolioError, setCreatePortfolioError] = useState('');
  const [changePasswordOpen, setChangePasswordOpen] = useState(false);
  const [changePasswordSuccess, setChangePasswordSuccess] = useState('');
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const summaryRequestRef = useRef(0);
  const performanceRequestRef = useRef(0);
  const aggregateSummaryRequestRef = useRef(0);
  const aggregatePerformanceRequestRef = useRef(0);
  const operationsRequestRef = useRef(0);
  const portfolioPage = route;
  const activePortfolio = portfolios.find((x) => x.id === selectedPortfolio) ?? null;
  const displayPositions = summary ? buildDisplayPositions(summary) : [];
  const currentSummary = viewMode === 'all' ? aggregateSummary : summary;
  const annualizedReturnLabel = viewMode === 'all'
    ? loadingAggregateSummary
      ? 'Годовая доходность: считаем...'
      : currentSummary?.annualizedReturnPct == null
        ? 'Годовая доходность: пока недостаточно данных'
        : `Годовая доходность: ${formatPercent(currentSummary.annualizedReturnPct)}`
    : loadingSummary
      ? 'Годовая доходность: считаем...'
      : currentSummary?.annualizedReturnPct == null
        ? 'Годовая доходность: пока недостаточно данных'
        : `Годовая доходность: ${formatPercent(currentSummary.annualizedReturnPct)}`;

  const loadPortfolios = useCallback(async (preferredId?: string) => {
    const data = await api.listPortfolios();
    setPortfolios(data);
    if (data.length) {
      setSelectedPortfolio((prev) => preferredId ?? prev ?? data[0].id);
      return;
    }

    setSelectedPortfolio(null);
    setSummary(null);
    setAggregateSummary(null);
    setPerformance([]);
    setAggregatePerformance([]);
    setOperations([]);
    setOperationsTotalCount(0);
    setOperationsPage(1);
    if (!isAdminReferencePage(route)) {
      onRouteChange('overview');
    }
    setViewMode('portfolio');
  }, [onRouteChange, route]);

  const loadBrokers = useCallback(async () => {
    const data = await api.listBrokers();
    setBrokers(data);
  }, []);

  const loadCurrencies = useCallback(async () => {
    const data = await api.listCurrencies();
    setCurrencies(data);
  }, []);

  const loadAggregateSummary = useCallback(async (method: string) => {
    const requestId = ++aggregateSummaryRequestRef.current;
    setLoadingAggregateSummary(true);
    setAggregateError('');
    try {
      const data = await api.getAggregatePortfolioSummary(method, activePortfolio?.reportingCurrencyId);
      if (aggregateSummaryRequestRef.current !== requestId) return;
      setAggregateSummary(data);
    } catch (error) {
      if (aggregateSummaryRequestRef.current !== requestId) return;
      setAggregateSummary(null);
      setAggregateError(getApiErrorMessage(error, 'Не удалось получить общий итог по всем счетам.'));
    } finally {
      if (aggregateSummaryRequestRef.current === requestId) {
        setLoadingAggregateSummary(false);
      }
    }
  }, [activePortfolio?.reportingCurrencyId]);

  const loadAggregatePerformance = useCallback(async (method: string) => {
    const requestId = ++aggregatePerformanceRequestRef.current;
    setLoadingAggregatePerformance(true);
    try {
      const data = await api.getAggregatePerformance(method, activePortfolio?.reportingCurrencyId);
      if (aggregatePerformanceRequestRef.current !== requestId) return;
      setAggregatePerformance(data);
    } catch (error) {
      if (aggregatePerformanceRequestRef.current !== requestId) return;
      console.error(error);
      setAggregatePerformance([]);
    } finally {
      if (aggregatePerformanceRequestRef.current === requestId) {
        setLoadingAggregatePerformance(false);
      }
    }
  }, [activePortfolio?.reportingCurrencyId]);

  const loadOperations = useCallback(async (id: string, page: number, pageSize: number) => {
    const requestId = ++operationsRequestRef.current;
    setLoadingOps(true);
    try {
      const data = await api.listOperations(id, { page, pageSize });
      if (operationsRequestRef.current !== requestId) return;
      setOperations(data.items);
      setOperationsTotalCount(data.totalCount);
      if (data.page !== page) setOperationsPage(data.page);
      if (data.pageSize !== pageSize) setOperationsPageSize(data.pageSize);
    } finally {
      if (operationsRequestRef.current === requestId) {
        setLoadingOps(false);
      }
    }
  }, []);

  useEffect(() => {
    (async () => {
      try {
        await Promise.all([loadPortfolios(), loadBrokers(), loadCurrencies()]);
      } catch (error) {
        console.error(error);
      }
    })();
  }, [loadBrokers, loadCurrencies, loadPortfolios]);

  useEffect(() => {
    if (viewMode !== 'all' || (portfolioPage !== 'overview' && portfolioPage !== 'analytics')) return;
    loadAggregateSummary(valuationMethod);
  }, [viewMode, valuationMethod, portfolioPage, loadAggregateSummary]);

  useEffect(() => {
    if (viewMode !== 'all' || portfolioPage !== 'analytics') return;
    loadAggregatePerformance(valuationMethod);
  }, [viewMode, valuationMethod, portfolioPage, loadAggregatePerformance]);

  useEffect(() => {
    if (viewMode !== 'portfolio' || (portfolioPage !== 'overview' && portfolioPage !== 'analytics')) return;
    if (!selectedPortfolio) return;

    loadSummary(selectedPortfolio, valuationMethod);
  }, [selectedPortfolio, valuationMethod, viewMode, portfolioPage]);

  useEffect(() => {
    if (viewMode !== 'portfolio' || portfolioPage !== 'analytics') return;
    if (!selectedPortfolio) return;

    loadPerformance(selectedPortfolio, valuationMethod);
  }, [selectedPortfolio, valuationMethod, viewMode, portfolioPage]);

  useEffect(() => {
    if (viewMode !== 'portfolio' || portfolioPage !== 'operations') return;
    if (!selectedPortfolio) return;

    loadOperations(selectedPortfolio, operationsPage, operationsPageSize);
  }, [selectedPortfolio, viewMode, portfolioPage, operationsPage, operationsPageSize, loadOperations]);

  async function loadSummary(id: string, method: string) {
    const requestId = ++summaryRequestRef.current;
    setLoadingSummary(true);
    try {
      const data = await api.getPortfolioSummary(id, method);
      if (summaryRequestRef.current !== requestId) return;
      setSummary(data);
    } catch (error) {
      if (summaryRequestRef.current !== requestId) return;
      console.error(error);
      setSummary(null);
    } finally {
      if (summaryRequestRef.current === requestId) {
        setLoadingSummary(false);
      }
    }
  }

  async function loadPerformance(id: string, method: string) {
    const requestId = ++performanceRequestRef.current;
    setLoadingPerformance(true);
    try {
      const data = await api.getPerformance(id, method);
      if (performanceRequestRef.current !== requestId) return;
      setPerformance(data);
    } catch (error) {
      if (performanceRequestRef.current !== requestId) return;
      console.error(error);
      setPerformance([]);
    } finally {
      if (performanceRequestRef.current === requestId) {
        setLoadingPerformance(false);
      }
    }
  }

  function handleOpenCreatePortfolio() {
    setCreatePortfolioError('');
    Promise.all([loadBrokers(), loadCurrencies()]).catch(console.error);
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

  function handleOpenChangePassword() {
    setSidebarOpen(false);
    setChangePasswordOpen(true);
  }

  function handleCloseChangePassword() {
    setChangePasswordOpen(false);
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

      return result;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Не удалось очистить данные портфеля.'));
    }
  }

  async function handleRecalculatePortfolio(): Promise<RecalculatePortfolioResult> {
    if (!selectedPortfolio) {
      throw new Error('Сначала выберите портфель.');
    }

    try {
      const result = await api.recalculatePortfolio(selectedPortfolio);

      if (portfolioPage === 'operations') {
        await loadOperations(selectedPortfolio, operationsPage, operationsPageSize);
      }

      return result;
    } catch (error) {
      throw new Error(getApiErrorMessage(error, 'Не удалось пересчитать портфель.'));
    }
  }

  const searchInstruments = useCallback((query: string): Promise<InstrumentLookup[]> => {
    return api.searchInstruments(query);
  }, []);

  function handleSelectPortfolio(id: string) {
    summaryRequestRef.current++;
    performanceRequestRef.current++;
    operationsRequestRef.current++;
    setViewMode('portfolio');
    setSelectedPortfolio(id);
    setSummary(null);
    setPerformance([]);
    setOperations([]);
    setOperationsTotalCount(0);
    setAggregateError('');
    setOperationsPage(1);
    setSidebarOpen(false);
  }

  function handleShowAllSummary() {
    setViewMode('all');
    onRouteChange('overview');
    setSidebarOpen(false);
  }

  function handleShowAllAnalytics() {
    setViewMode('all');
    onRouteChange('analytics');
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

  function handleShowAnalytics() {
    if (!selectedPortfolio) return;
    setViewMode('portfolio');
    onRouteChange('analytics');
    setSidebarOpen(false);
  }

  function handleShowAdminInstruments() {
    if (!user.isAdmin) return;
    onRouteChange('instruments');
    setSidebarOpen(false);
  }

  function handleShowAdminCurrencies() {
    if (!user.isAdmin) return;
    onRouteChange('currencies');
    setSidebarOpen(false);
  }

  const activeBroker = brokers.find((x) => x.id === activePortfolio?.brokerId) ?? null;
  const canImportOperations = Boolean(activeBroker?.supportsImport && activeBroker.code);
  const performanceCurrency = performance[0]?.reportingCurrencyId;
  const aggregatePerformanceCurrency = aggregatePerformance[0]?.reportingCurrencyId;
  const currency =
    viewMode === 'all'
      ? aggregatePerformanceCurrency ?? aggregateSummary?.reportingCurrencyId ?? activePortfolio?.reportingCurrencyId ?? '—'
      : portfolioPage === 'analytics'
        ? performanceCurrency ?? activePortfolio?.reportingCurrencyId ?? summary?.reportingCurrencyId ?? '—'
        : summary?.reportingCurrencyId ?? activePortfolio?.reportingCurrencyId ?? '—';
  const isLoadingCurrent =
    viewMode === 'all'
      ? loadingAggregateSummary || (portfolioPage === 'analytics' && loadingAggregatePerformance)
      : portfolioPage === 'overview'
        ? loadingSummary
        : portfolioPage === 'analytics'
          ? loadingSummary || loadingPerformance
          : false;

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
            isAdmin={user.isAdmin}
            adminInstrumentsSelected={portfolioPage === 'instruments'}
            adminCurrenciesSelected={portfolioPage === 'currencies'}
            onShowAdminInstruments={handleShowAdminInstruments}
            onShowAdminCurrencies={handleShowAdminCurrencies}
            onChangePassword={handleOpenChangePassword}
            onLogout={onLogout}
          />
        </Box>
      )}
      <Drawer
        anchor="left"
        open={isMobile && sidebarOpen}
        onClose={() => setSidebarOpen(false)}
        slotProps={{
          paper: {
            sx: {
              width: 'min(86vw, 320px)',
              bgcolor: 'background.paper',
              backgroundImage: 'none',
            },
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
          isAdmin={user.isAdmin}
          adminInstrumentsSelected={portfolioPage === 'instruments'}
          adminCurrenciesSelected={portfolioPage === 'currencies'}
          onShowAdminInstruments={handleShowAdminInstruments}
          onShowAdminCurrencies={handleShowAdminCurrencies}
          onChangePassword={handleOpenChangePassword}
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
              spacing={1.5}
              sx={{ position: 'relative', zIndex: 1, alignItems: { xs: 'stretch', md: 'center' }, justifyContent: 'space-between' }}
            >
              <Stack spacing={0.5}>
                <Typography variant="overline" color="text.secondary">
                  {isAdminReferencePage(portfolioPage)
                    ? 'Администрирование'
                    : viewMode === 'all'
                      ? 'Режим просмотра'
                      : portfolioPage === 'operations'
                        ? 'Операции портфеля'
                        : 'Активный портфель'}
                </Typography>
                <Typography variant="h5" sx={{ fontSize: { xs: '1.25rem', sm: '1.5rem' }, fontWeight: 700 }}>
                  {portfolioPage === 'instruments'
                    ? 'Справочник инструментов'
                    : portfolioPage === 'currencies'
                      ? 'Справочник валют'
                    : viewMode === 'all'
                      ? 'Все счета'
                      : activePortfolio?.name ?? 'Выберите портфель'}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {isAdminReferencePage(portfolioPage)
                    ? 'Создание и редактирование доступно только администраторам.'
                    : `Валюта отчета: ${currency}`}
                </Typography>
                {!isAdminReferencePage(portfolioPage) && (
                  <Typography variant="body2" color="text.secondary">
                    {annualizedReturnLabel}
                  </Typography>
                )}
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
                    startIcon={<AddCircleOutlinedIcon />}
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
                    <Button
                      variant={portfolioPage === 'analytics' ? 'contained' : 'outlined'}
                      onClick={handleShowAnalytics}
                      sx={{ textTransform: 'none' }}
                      fullWidth={isMobile}
                    >
                      Аналитика
                    </Button>
                  </Stack>
                )}
                {viewMode === 'all' && (
                  <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ width: { xs: '100%', md: 'auto' } }}>
                    <Button
                      variant={portfolioPage === 'overview' ? 'contained' : 'outlined'}
                      onClick={handleShowAllSummary}
                      sx={{ textTransform: 'none' }}
                      fullWidth={isMobile}
                    >
                      Обзор
                    </Button>
                    <Button
                      variant={portfolioPage === 'analytics' ? 'contained' : 'outlined'}
                      onClick={handleShowAllAnalytics}
                      sx={{ textTransform: 'none' }}
                      fullWidth={isMobile}
                    >
                      Аналитика
                    </Button>
                  </Stack>
                )}
                {!isAdminReferencePage(portfolioPage) && (
                  <Stack
                    direction={{ xs: 'column', sm: 'row' }}
                    spacing={1}
                    sx={{ width: { xs: '100%', md: 'auto' }, alignItems: { xs: 'stretch', sm: 'center' } }}
                  >
                    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ alignItems: { xs: 'stretch', sm: 'center' } }}>
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
                      <Typography variant="subtitle1" sx={{ fontWeight: 700, lineHeight: 1.2 }}>
                        {currency}
                      </Typography>
                    </Paper>
                  </Stack>
                )}
              </Stack>
            </Stack>
          </Paper>

          {isLoadingCurrent && (
            <Stack sx={{ py: 4, alignItems: 'center' }}>
              <CircularProgress />
            </Stack>
          )}

          {portfolioPage === 'instruments' && user.isAdmin && (
            <Suspense
              fallback={
                <Stack sx={{ py: 4, alignItems: 'center' }}>
                  <CircularProgress />
                </Stack>
              }
            >
              <AdminInstrumentsPage />
            </Suspense>
          )}

          {portfolioPage === 'currencies' && user.isAdmin && (
            <Suspense
              fallback={
                <Stack sx={{ py: 4, alignItems: 'center' }}>
                  <CircularProgress />
                </Stack>
              }
            >
              <AdminCurrenciesPage />
            </Suspense>
          )}

          {viewMode === 'portfolio' && portfolioPage === 'overview' && !loadingSummary && summary && (
            <Stack spacing={{ xs: 2, md: 3 }}>
              <SummaryCards summary={summary} />

              <Grid container spacing={{ xs: 2, md: 3 }}>
                <Grid size={{ xs: 12, md: 8 }}>
                  <Stack spacing={1}>
                    <Typography variant="h6" sx={{ fontWeight: 700 }}>
                      Позиции
                    </Typography>
                    <PositionsTable
                      positions={displayPositions}
                      reportingCurrencyId={summary.reportingCurrencyId}
                      dailyPnlBase={summary.dailyMove?.dataQuality === 'complete' ? summary.dailyMove.pnlBase : undefined}
                    />
                  </Stack>
                </Grid>
                <Grid size={{ xs: 12, md: 4 }}>
                  <Paper variant="outlined" sx={{ p: { xs: 1.5, sm: 2 }, height: '100%', backgroundImage: 'none' }}>
                    <QuickDeposit
                      key={selectedPortfolio ?? 'no-portfolio'}
                      onSubmit={handleQuickDeposit}
                      currencies={currencies}
                      defaultCurrencyId={activePortfolio?.reportingCurrencyId}
                      disabled={!selectedPortfolio}
                    />
                  </Paper>
                </Grid>
              </Grid>

              <Stack spacing={1}>
                <Typography variant="h6" sx={{ fontWeight: 700 }}>
                  Аналитика
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Доходность и графики вынесены на отдельную страницу, чтобы обзор портфеля загружался быстрее.
                </Typography>
                <Button variant="outlined" onClick={handleShowAnalytics} sx={{ alignSelf: 'flex-start' }}>
                  Открыть аналитику
                </Button>
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
                onRecalculatePortfolio={handleRecalculatePortfolio}
                canImport={canImportOperations}
                importDisabledReason="Для брокера выбранного счета импорт пока не настроен."
                onUpdate={handleUpdateOperation}
                onDelete={handleDeleteOperation}
                searchInstruments={searchInstruments}
              />
            </Stack>
          )}

          {viewMode === 'portfolio' && portfolioPage === 'analytics' && !loadingSummary && !loadingPerformance && selectedPortfolio && (
            <PerformanceAnalytics
              summary={summary}
              items={performance}
              currency={performanceCurrency ?? activePortfolio?.reportingCurrencyId ?? summary?.reportingCurrencyId ?? currency}
            />
          )}

          {viewMode === 'all' && portfolioPage === 'overview' && !loadingAggregateSummary && aggregateSummary && (
            <Stack spacing={{ xs: 2, md: 3 }}>
              <SummaryCards summary={aggregateSummary} />

              <Stack spacing={1}>
                <Typography variant="h6" sx={{ fontWeight: 700 }}>
                  Активы по всем счетам ({portfolios.length})
                </Typography>
                <PositionsTable
                  positions={buildDisplayPositions(aggregateSummary)}
                  reportingCurrencyId={aggregateSummary.reportingCurrencyId}
                  dailyPnlBase={aggregateSummary.dailyMove?.dataQuality === 'complete' ? aggregateSummary.dailyMove.pnlBase : undefined}
                />
              </Stack>

              <Stack spacing={1}>
                <Typography variant="h6" sx={{ fontWeight: 700 }}>
                  Аналитика
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Общая структура и прибыль по всем брокерам в одной базовой валюте.
                </Typography>
                <Button variant="outlined" onClick={handleShowAllAnalytics} sx={{ alignSelf: 'flex-start' }}>
                  Открыть аналитику
                </Button>
              </Stack>
            </Stack>
          )}

          {viewMode === 'all' && portfolioPage === 'analytics' && !loadingAggregateSummary && !loadingAggregatePerformance && aggregateSummary && (
            <PerformanceAnalytics
              summary={aggregateSummary}
              items={aggregatePerformance}
              currency={aggregatePerformanceCurrency ?? aggregateSummary.reportingCurrencyId ?? currency}
            />
          )}

          {viewMode === 'all' && !loadingAggregateSummary && !aggregateSummary && aggregateError && (
            <Paper variant="outlined" sx={{ p: { xs: 2, md: 3 }, textAlign: 'center', backgroundImage: 'none' }}>
              <Typography color="error.main">{aggregateError}</Typography>
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
        currencies={currencies}
        submitting={createPortfolioLoading}
        error={createPortfolioError}
        onClose={handleCloseCreatePortfolio}
        onSubmit={handleCreatePortfolio}
      />
      <ChangePasswordDialog
        open={changePasswordOpen}
        onClose={handleCloseChangePassword}
        onSuccess={() => setChangePasswordSuccess('Пароль обновлен.')}
      />
      <Snackbar
        open={Boolean(changePasswordSuccess)}
        autoHideDuration={3000}
        onClose={() => setChangePasswordSuccess('')}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert onClose={() => setChangePasswordSuccess('')} severity="success" sx={{ width: '100%' }}>
          {changePasswordSuccess}
        </Alert>
      </Snackbar>
    </Box>
  );
}
