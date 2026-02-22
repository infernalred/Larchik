import { useEffect, useState } from 'react';
import {
  Box,
  Container,
  Grid,
  MenuItem,
  Paper,
  Select,
  Stack,
  Typography,
  CircularProgress,
} from '@mui/material';
import { api } from './api';
import { Broker, InstrumentLookup, Operation, OperationModel, Portfolio, PortfolioPerformance, PortfolioSummary } from './types';
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
}

function getApiErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof Error)) return fallback;
  try {
    const payload = JSON.parse(error.message) as { message?: string };
    return payload.message || fallback;
  } catch {
    return error.message || fallback;
  }
}

export function Dashboard({ onLogout }: Props) {
  const [portfolios, setPortfolios] = useState<Portfolio[]>([]);
  const [brokers, setBrokers] = useState<Broker[]>([]);
  const [selectedPortfolio, setSelectedPortfolio] = useState<string | null>(null);
  const [summary, setSummary] = useState<PortfolioSummary | null>(null);
  const [performance, setPerformance] = useState<PortfolioPerformance[]>([]);
  const [valuationMethod, setValuationMethod] = useState('adjustingAvg');
  const [loadingSummary, setLoadingSummary] = useState(false);
  const [operations, setOperations] = useState<Operation[]>([]);
  const [loadingOps, setLoadingOps] = useState(false);
  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const [createPortfolioLoading, setCreatePortfolioLoading] = useState(false);
  const [createPortfolioError, setCreatePortfolioError] = useState('');

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
    if (!selectedPortfolio) return;
    loadSummary(selectedPortfolio, valuationMethod);
    loadPerformance(selectedPortfolio, valuationMethod);
    loadOperations(selectedPortfolio);
  }, [selectedPortfolio, valuationMethod]);

  async function loadPortfolios(preferredId?: string) {
    const data = await api.listPortfolios();
    setPortfolios(data);
    if (data.length) {
      setSelectedPortfolio((prev) => preferredId ?? prev ?? data[0].id);
    }
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

  async function loadPerformance(id: string, method: string) {
    const data = await api.getPerformance(id, method);
    setPerformance(data);
  }

  async function loadOperations(id: string) {
    setLoadingOps(true);
    try {
      const data = await api.listOperations(id);
      setOperations(data);
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
    await loadOperations(selectedPortfolio);
  }

  async function handleCreateOperation(model: OperationModel) {
    if (!selectedPortfolio) return;
    await api.createOperation(selectedPortfolio, model);
    await loadSummary(selectedPortfolio, valuationMethod);
    await loadPerformance(selectedPortfolio, valuationMethod);
    await loadOperations(selectedPortfolio);
  }

  async function handleUpdateOperation(id: string, model: OperationModel) {
    if (!selectedPortfolio) return;
    await api.updateOperation(selectedPortfolio, id, model);
    await loadSummary(selectedPortfolio, valuationMethod);
    await loadPerformance(selectedPortfolio, valuationMethod);
    await loadOperations(selectedPortfolio);
  }

  async function handleDeleteOperation(id: string) {
    if (!selectedPortfolio) return;
    await api.deleteOperation(selectedPortfolio, id);
    await loadSummary(selectedPortfolio, valuationMethod);
    await loadPerformance(selectedPortfolio, valuationMethod);
    await loadOperations(selectedPortfolio);
  }

  async function searchInstruments(query: string): Promise<InstrumentLookup[]> {
    return api.searchInstruments(query);
  }

  const currency = summary?.reportingCurrencyId ?? '—';

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', color: 'text.primary' }}>
      <Box sx={{ bgcolor: 'rgba(255,255,255,0.02)', borderRight: '1px solid rgba(255,255,255,0.06)' }}>
        <PortfolioSidebar
          items={portfolios}
          selectedId={selectedPortfolio}
          onSelect={setSelectedPortfolio}
          onCreate={handleOpenCreatePortfolio}
          onLogout={onLogout}
        />
      </Box>
      <Box sx={{ flex: 1 }}>
        <Container sx={{ py: 3 }}>
          <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 2 }}>
            <div>
              <Typography variant="overline" color="text.secondary">
                Метод оценки
              </Typography>
              <Select
                size="small"
                value={valuationMethod}
                onChange={(e) => setValuationMethod(e.target.value)}
                sx={{ ml: 1, minWidth: 160 }}
              >
                {VALUATION_METHODS.map((m) => (
                  <MenuItem key={m.value} value={m.value}>
                    {m.label}
                  </MenuItem>
                ))}
              </Select>
            </div>
            <Paper variant="outlined" sx={{ px: 2, py: 1, borderRadius: 999 }}>
              <Typography variant="body2" color="text.secondary">
                Базовая валюта
              </Typography>
              <Typography variant="subtitle1" fontWeight={700}>
                {currency}
              </Typography>
            </Paper>
          </Stack>

          {loadingSummary && (
            <Stack alignItems="center" sx={{ py: 4 }}>
              <CircularProgress />
            </Stack>
          )}

          {!loadingSummary && summary && (
            <Stack spacing={3}>
              <SummaryCards summary={summary} />

              <Grid container spacing={3}>
                <Grid item xs={12} md={8}>
                  <Stack spacing={1}>
                    <Typography variant="h6" fontWeight={700}>
                      Позиции
                    </Typography>
                    <PositionsTable positions={summary.positions} />
                  </Stack>
                </Grid>
                <Grid item xs={12} md={4}>
                  <Paper variant="outlined" sx={{ p: 2, height: '100%', backgroundImage: 'none' }}>
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

              <Stack spacing={1}>
                <Typography variant="h6" fontWeight={700}>
                  Операции
                </Typography>
                {loadingOps && <Typography color="text.secondary">Загрузка операций…</Typography>}
                <OperationsPanel
                  items={operations}
                  onCreate={handleCreateOperation}
                  onUpdate={handleUpdateOperation}
                  onDelete={handleDeleteOperation}
                  searchInstruments={searchInstruments}
                />
              </Stack>
            </Stack>
          )}

          {!loadingSummary && !summary && (
            <Paper variant="outlined" sx={{ p: 3, textAlign: 'center', backgroundImage: 'none' }}>
              <Typography color="text.secondary">Выберите портфель или создайте новый</Typography>
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
