import { Paper, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material';
import { PortfolioPerformance } from './types';

interface Props {
  items: PortfolioPerformance[];
}

const fmt = (v: number | null | undefined) =>
  v == null ? '—' : v.toLocaleString('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export function PerformanceTable({ items }: Props) {
  return (
    <TableContainer component={Paper} variant="outlined" sx={{ backgroundImage: 'none' }}>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Месяц</TableCell>
            <TableCell align="right">Начало NAV</TableCell>
            <TableCell align="right">Конец NAV</TableCell>
            <TableCell align="right">Потоки</TableCell>
            <TableCell align="right">P&L</TableCell>
            <TableCell align="right">Доходность</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {items.map((row) => (
            <TableRow key={row.period} hover>
              <TableCell>
                <Typography fontWeight={600}>{row.period}</Typography>
              </TableCell>
              <TableCell align="right">{fmt(row.startNavBase)}</TableCell>
              <TableCell align="right">{fmt(row.endNavBase)}</TableCell>
              <TableCell align="right">{fmt(row.netInflowBase)}</TableCell>
              <TableCell align="right">{fmt(row.pnlBase)}</TableCell>
              <TableCell align="right">
                {row.returnPct == null ? (
                  '—'
                ) : (
                  <Typography color={row.returnPct >= 0 ? 'success.main' : 'error.main'}>
                    {(row.returnPct * 100).toFixed(2)}%
                  </Typography>
                )}
              </TableCell>
            </TableRow>
          ))}
          {!items.length && (
            <TableRow>
              <TableCell colSpan={6} align="center">
                <Typography color="text.secondary">Нет данных</Typography>
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
