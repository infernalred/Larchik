import { useMemo, useState } from 'react';
import {
  Box,
  IconButton,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
  Button,
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
  const [editing, setEditing] = useState<Operation | null>(null);
  const [creating, setCreating] = useState(false);

  const sorted = useMemo(
    () => [...items].sort((a, b) => b.tradeDate.localeCompare(a.tradeDate)),
    [items],
  );

  return (
    <Paper variant="outlined" sx={{ p: 2, backgroundImage: 'none' }}>
      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1 }}>
        <Typography variant="h6" fontWeight={700}>
          Операции
        </Typography>
        <Button startIcon={<AddIcon />} variant="contained" onClick={() => setCreating(true)} sx={{ textTransform: 'none' }}>
          Новая операция
        </Button>
      </Stack>
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
