import { useMemo, useState } from 'react';
import {
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
import { InstrumentLookup, Operation, OperationModel } from './types';
import { OperationForm } from './OperationForm';

interface Props {
  items: Operation[];
  onCreate: (model: OperationModel) => Promise<void>;
  onUpdate: (id: string, model: OperationModel) => Promise<void>;
  onDelete: (id: string) => Promise<void>;
  searchInstruments: (query: string) => Promise<InstrumentLookup[]>;
}

const fmtDate = (v: string) => v.slice(0, 10);
const fmtNum = (v: number | null | undefined) => (v == null ? '—' : v.toLocaleString('ru-RU', { maximumFractionDigits: 4 }));

export function OperationsPanel({ items, onCreate, onUpdate, onDelete, searchInstruments }: Props) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'));
  const [editing, setEditing] = useState<Operation | null>(null);
  const [creating, setCreating] = useState(false);

  const sorted = useMemo(
    () => [...items].sort((a, b) => b.tradeDate.localeCompare(a.tradeDate)),
    [items],
  );

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
        <Button
          startIcon={<AddIcon />}
          variant="contained"
          onClick={() => setCreating(true)}
          sx={{ textTransform: 'none', alignSelf: { xs: 'stretch', sm: 'auto' } }}
        >
          Новая операция
        </Button>
      </Stack>
      {isMobile ? (
        <Stack spacing={1.25}>
          {sorted.map((op) => (
            <Paper key={op.id} variant="outlined" sx={{ p: 1.25, backgroundImage: 'none' }}>
              <Stack spacing={1.25}>
                <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1}>
                  <Stack spacing={0.25}>
                    <Typography fontWeight={700}>{op.type}</Typography>
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
                  <Grid item xs={4}>
                    <Typography variant="caption" color="text.secondary">
                      Кол-во
                    </Typography>
                    <Typography variant="body2">{fmtNum(op.quantity)}</Typography>
                  </Grid>
                  <Grid item xs={4}>
                    <Typography variant="caption" color="text.secondary">
                      Цена/сумма
                    </Typography>
                    <Typography variant="body2">{fmtNum(op.price)}</Typography>
                  </Grid>
                  <Grid item xs={4}>
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
          {!sorted.length && (
            <Paper variant="outlined" sx={{ p: 2, textAlign: 'center', backgroundImage: 'none' }}>
              <Typography color="text.secondary">Нет операций</Typography>
            </Paper>
          )}
        </Stack>
      ) : (
        <TableContainer component={Box}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Дата</TableCell>
                <TableCell>Тип</TableCell>
                <TableCell>Инструмент</TableCell>
                <TableCell align="right">Кол-во</TableCell>
                <TableCell align="right">Цена/сумма</TableCell>
                <TableCell align="right">Комиссия</TableCell>
                <TableCell align="right">Валюта</TableCell>
                <TableCell align="right">Комментарий</TableCell>
                <TableCell align="right">Действия</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {sorted.map((op) => (
                <TableRow key={op.id} hover>
                  <TableCell>{fmtDate(op.tradeDate)}</TableCell>
                  <TableCell>{op.type}</TableCell>
                  <TableCell>{op.instrumentTicker ?? '—'}</TableCell>
                  <TableCell align="right">{fmtNum(op.quantity)}</TableCell>
                  <TableCell align="right">{fmtNum(op.price)}</TableCell>
                  <TableCell align="right">{fmtNum(op.fee)}</TableCell>
                  <TableCell align="right">{op.currencyId}</TableCell>
                  <TableCell align="right">
                    <Typography variant="body2" color="text.secondary" noWrap maxWidth={160}>
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
              {!sorted.length && (
                <TableRow>
                  <TableCell colSpan={9} align="center">
                    <Typography color="text.secondary">Нет операций</Typography>
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

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
    </Paper>
  );
}
