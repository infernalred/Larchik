import { Paper, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material';
import { PositionHolding } from './types';

interface Props {
  positions: PositionHolding[];
}

const fmt = (v: number | null | undefined) =>
  v == null ? '—' : v.toLocaleString('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export function PositionsTable({ positions }: Props) {
  return (
    <TableContainer component={Paper} variant="outlined" sx={{ backgroundImage: 'none' }}>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Инструмент</TableCell>
            <TableCell align="right">Кол-во</TableCell>
            <TableCell align="right">Средняя</TableCell>
            <TableCell align="right">Последняя</TableCell>
            <TableCell align="right">Стоимость (base)</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {positions.map((p) => (
            <TableRow key={p.instrumentId} hover>
              <TableCell>
                <Typography fontWeight={600}>{p.instrumentName || '—'}</Typography>
                <Typography variant="caption" color="text.secondary">
                  {p.currencyId || '—'}
                </Typography>
              </TableCell>
              <TableCell align="right">{fmt(p.quantity)}</TableCell>
              <TableCell align="right">
                {fmt(p.averageCost)} {p.currencyId}
              </TableCell>
              <TableCell align="right">
                {p.lastPrice != null ? `${fmt(p.lastPrice)} ${p.currencyId}` : '—'}
              </TableCell>
              <TableCell align="right">{fmt(p.marketValueBase)}</TableCell>
            </TableRow>
          ))}
          {!positions.length && (
            <TableRow>
              <TableCell colSpan={5} align="center">
                <Typography color="text.secondary">Нет позиций</Typography>
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
