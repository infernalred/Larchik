import { useState } from 'react';
import {
  Alert,
  Box,
  IconButton,
  Paper,
  Stack,
  Grid,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  Tooltip,
  Typography,
  Button,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/Delete';
import AddIcon from '@mui/icons-material/Add';
import DeleteSweepIcon from '@mui/icons-material/DeleteSweep';
import {
  ClearPortfolioDataResult,
  ImportResult,
  InstrumentLookup,
  Operation,
  OperationModel,
  OperationType,
} from './types';
import { ImportOperationsDialog } from './ImportOperationsDialog';
import { OperationForm } from './OperationForm';

interface Props {
  items: Operation[];
  loading?: boolean;
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
  onCreate: (model: OperationModel) => Promise<void>;
  onImport: (file: File) => Promise<ImportResult>;
  onClearPortfolioData: () => Promise<ClearPortfolioDataResult>;
  canImport: boolean;
  importDisabledReason?: string;
  onUpdate: (id: string, model: OperationModel) => Promise<void>;
  onDelete: (id: string) => Promise<void>;
  searchInstruments: (query: string) => Promise<InstrumentLookup[]>;
}

const fmtDate = (v: string) => v.slice(0, 10);
const fmtNum = (v: number | null | undefined) => (v == null ? '—' : v.toLocaleString('ru-RU', { maximumFractionDigits: 4 }));
const calcAmount = (op: Operation) => op.quantity * op.price;
const TYPE_LABELS: Record<OperationType, string> = {
  Buy: 'Покупка',
  Sell: 'Продажа',
  Dividend: 'Дивиденд',
  Fee: 'Комиссия',
  Deposit: 'Депозит',
  Withdraw: 'Вывод',
  TransferIn: 'Перевод в',
  TransferOut: 'Перевод из',
  BondPartialRedemption: 'Частичное погашение',
  BondMaturity: 'Погашение',
  Split: 'Сплит',
  ReverseSplit: 'Обратный сплит',
  CashAdjustment: 'Движение денег',
};

export function OperationsPanel({
  items,
  loading = false,
  page,
  pageSize,
  totalCount,
  onPageChange,
  onPageSizeChange,
  onCreate,
  onImport,
  onClearPortfolioData,
  canImport,
  importDisabledReason,
  onUpdate,
  onDelete,
  searchInstruments,
}: Props) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'));
  const [editing, setEditing] = useState<Operation | null>(null);
  const [creating, setCreating] = useState(false);
  const [importing, setImporting] = useState(false);
  const [clearing, setClearing] = useState(false);
  const [importDialogOpen, setImportDialogOpen] = useState(false);
  const [importError, setImportError] = useState('');
  const [importSummary, setImportSummary] = useState('');

  const handleImport = async (file: File) => {
    setImporting(true);
    setImportError('');
    setImportSummary('');
    try {
      const result = await onImport(file);
      const details = [`Импортировано операций: ${result.importedOperations}`];
      if (result.errors.length > 0) {
        details.push(`Замечания: ${result.errors.join('; ')}`);
      }

      setImportSummary(details.join('. '));
      setImportDialogOpen(false);
    } catch (error) {
      setImportError(error instanceof Error ? error.message : 'Не удалось импортировать отчет.');
    } finally {
      setImporting(false);
    }
  };

  const handleClearPortfolioData = async () => {
    const confirmed = window.confirm(
      'Очистить все операции и пересчитанные данные выбранного портфеля? Это действие нельзя отменить.',
    );
    if (!confirmed) return;

    setClearing(true);
    setImportError('');
    setImportSummary('');
    try {
      const result = await onClearPortfolioData();
      setImportSummary(
        [
          `Операций удалено: ${result.deletedOperations}`,
          `Снапшотов позиций удалено: ${result.deletedPositionSnapshots}`,
          `Снапшотов портфеля удалено: ${result.deletedPortfolioSnapshots}`,
        ].join('. '),
      );
    } catch (error) {
      setImportError(error instanceof Error ? error.message : 'Не удалось очистить портфель.');
    } finally {
      setClearing(false);
    }
  };

  return (
    <Paper variant="outlined" sx={{ p: { xs: 1.5, sm: 2 }, backgroundImage: 'none' }}>
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        justifyContent="space-between"
        alignItems={{ xs: 'stretch', sm: 'center' }}
        spacing={1}
        sx={{ mb: 1.5 }}
      >
        <Typography variant="h6" fontWeight={700}>
          Операции
        </Typography>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
          {totalCount > 0 && (
            <Button
              startIcon={<DeleteSweepIcon />}
              variant="outlined"
              color="warning"
              onClick={handleClearPortfolioData}
              disabled={clearing || importing}
              sx={{ textTransform: 'none', alignSelf: { xs: 'stretch', sm: 'auto' } }}
            >
              Очистить портфель
            </Button>
          )}
          <Button
            variant="outlined"
            onClick={() => {
              if (!canImport) return;
              setImportError('');
              setImportDialogOpen(true);
            }}
            disabled={!canImport || clearing}
            title={!canImport ? importDisabledReason : undefined}
            sx={{ textTransform: 'none', alignSelf: { xs: 'stretch', sm: 'auto' } }}
          >
            Импорт отчета
          </Button>
          <Button
            startIcon={<AddIcon />}
            variant="contained"
            onClick={() => setCreating(true)}
            disabled={clearing}
            sx={{ textTransform: 'none', alignSelf: { xs: 'stretch', sm: 'auto' } }}
          >
            Новая операция
          </Button>
        </Stack>
      </Stack>
      {importSummary && (
        <Alert severity="success" sx={{ mb: 1.5 }}>
          {importSummary}
        </Alert>
      )}
      {loading && (
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
          Загрузка операций...
        </Typography>
      )}
      {isMobile ? (
        <Stack spacing={1.25}>
          {items.map((op) => (
            <Paper key={op.id} variant="outlined" sx={{ p: 1.25, backgroundImage: 'none' }}>
              <Stack spacing={1.25}>
                <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1}>
                  <Stack spacing={0.25}>
                    <Typography fontWeight={700}>{TYPE_LABELS[op.type]}</Typography>
                    <Typography variant="caption" color="text.secondary">
                      {fmtDate(op.tradeDate)}
                    </Typography>
                  </Stack>
                  <Stack direction="row" spacing={0.5}>
                    <Tooltip title="Редактировать">
                      <IconButton size="small" onClick={() => setEditing(op)}>
                        <EditIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Удалить">
                      <IconButton size="small" onClick={() => onDelete(op.id)}>
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </Stack>
                </Stack>
                <Grid container spacing={1}>
                  <Grid item xs={6}>
                    <Typography variant="caption" color="text.secondary">
                      Инструмент
                    </Typography>
                    <Typography variant="body2">{op.instrumentTicker ?? '—'}</Typography>
                  </Grid>
                  <Grid item xs={6}>
                    <Typography variant="caption" color="text.secondary">
                      Валюта
                    </Typography>
                    <Typography variant="body2">{op.currencyId}</Typography>
                  </Grid>
                  <Grid item xs={6}>
                    <Typography variant="caption" color="text.secondary">
                      Кол-во
                    </Typography>
                    <Typography variant="body2">{fmtNum(op.quantity)}</Typography>
                  </Grid>
                  <Grid item xs={6}>
                    <Typography variant="caption" color="text.secondary">
                      Цена
                    </Typography>
                    <Typography variant="body2">{fmtNum(op.price)}</Typography>
                  </Grid>
                  <Grid item xs={6}>
                    <Typography variant="caption" color="text.secondary">
                      Сумма
                    </Typography>
                    <Typography variant="body2">{fmtNum(calcAmount(op))}</Typography>
                  </Grid>
                  <Grid item xs={6}>
                    <Typography variant="caption" color="text.secondary">
                      Комиссия
                    </Typography>
                    <Typography variant="body2">{fmtNum(op.fee)}</Typography>
                  </Grid>
                  <Grid item xs={12}>
                    <Typography variant="caption" color="text.secondary">
                      Комментарий
                    </Typography>
                    <Typography variant="body2" color="text.secondary">
                      {op.note ?? '—'}
                    </Typography>
                  </Grid>
                </Grid>
              </Stack>
            </Paper>
          ))}
          {!items.length && !loading && (
            <Paper variant="outlined" sx={{ p: 2, textAlign: 'center', backgroundImage: 'none' }}>
              <Typography color="text.secondary">Нет операций</Typography>
            </Paper>
          )}
        </Stack>
      ) : (
        <TableContainer component={Box} sx={{ borderRadius: 2, maxHeight: 520 }}>
          <Table size="small" stickyHeader>
            <TableHead>
              <TableRow>
                <TableCell>Дата</TableCell>
                <TableCell>Тип</TableCell>
                <TableCell>Инструмент</TableCell>
                <TableCell align="right">Кол-во</TableCell>
                <TableCell align="right">Цена</TableCell>
                <TableCell align="right">Сумма</TableCell>
                <TableCell align="right">Комиссия</TableCell>
                <TableCell align="right">Валюта</TableCell>
                <TableCell>Комментарий</TableCell>
                <TableCell align="right">Действия</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {items.map((op) => (
                <TableRow key={op.id} hover>
                  <TableCell>{fmtDate(op.tradeDate)}</TableCell>
                  <TableCell>{TYPE_LABELS[op.type]}</TableCell>
                  <TableCell>{op.instrumentTicker ?? '—'}</TableCell>
                  <TableCell align="right">{fmtNum(op.quantity)}</TableCell>
                  <TableCell align="right">{fmtNum(op.price)}</TableCell>
                  <TableCell align="right">{fmtNum(calcAmount(op))}</TableCell>
                  <TableCell align="right">{fmtNum(op.fee)}</TableCell>
                  <TableCell align="right">{op.currencyId}</TableCell>
                  <TableCell>
                    <Typography variant="body2" color="text.secondary" noWrap maxWidth={240}>
                      {op.note ?? '—'}
                    </Typography>
                  </TableCell>
                  <TableCell align="right">
                    <Tooltip title="Редактировать">
                      <IconButton size="small" onClick={() => setEditing(op)}>
                        <EditIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Удалить">
                      <IconButton size="small" onClick={() => onDelete(op.id)}>
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              ))}
              {!items.length && !loading && (
                <TableRow>
                  <TableCell colSpan={10} align="center">
                    <Typography color="text.secondary">Нет операций</Typography>
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}
      <TablePagination
        component="div"
        count={totalCount}
        page={Math.max(0, page - 1)}
        onPageChange={(_, nextPage) => onPageChange(nextPage + 1)}
        rowsPerPage={pageSize}
        onRowsPerPageChange={(event) => onPageSizeChange(Number(event.target.value))}
        rowsPerPageOptions={[10, 25, 50, 100]}
        labelRowsPerPage="На странице:"
        labelDisplayedRows={({ from, to, count }) => `${from}-${to} из ${count}`}
      />

      <OperationForm
        open={creating}
        onClose={() => setCreating(false)}
        searchInstruments={searchInstruments}
        onSubmit={async (model) => {
          await onCreate(model);
          setCreating(false);
        }}
      />

      <OperationForm
        open={Boolean(editing)}
        initial={editing ?? undefined}
        onClose={() => setEditing(null)}
        searchInstruments={searchInstruments}
        onSubmit={async (model) => {
          if (!editing) return;
          await onUpdate(editing.id, model);
          setEditing(null);
        }}
      />

      <ImportOperationsDialog
        open={importDialogOpen}
        submitting={importing}
        error={importError}
        canSubmit={canImport}
        disabledReason={importDisabledReason}
        onClose={() => {
          if (importing) return;
          setImportDialogOpen(false);
        }}
        onSubmit={handleImport}
      />
    </Paper>
  );
}
