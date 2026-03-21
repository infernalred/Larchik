import {
  Grid,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import { PositionHolding } from './types';

interface Props {
  positions: PositionHolding[];
}

const fmt = (v: number | null | undefined) =>
  v == null ? '—' : v.toLocaleString('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

const fmtPct = (v: number | null | undefined) =>
  v == null ? '—' : `${v.toLocaleString('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}%`;

function buildCurrencyTotals(positions: PositionHolding[]) {
  const totals = new Map<string, number>();

  for (const position of positions) {
    const localAmount = position.localAmount ?? (position.lastPrice != null ? position.quantity * position.lastPrice : null);
    if (localAmount == null) {
      continue;
    }

    totals.set(position.currencyId, (totals.get(position.currencyId) ?? 0) + localAmount);
  }

  return [...totals.entries()].map(([currencyId, amount]) => ({
    currencyId,
    amount,
  }));
}

export function PositionsTable({ positions }: Props) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'));
  const totalBase = positions.reduce((sum, p) => sum + p.marketValueBase, 0);
  const totalByCurrency = buildCurrencyTotals(positions);

  if (isMobile) {
    return (
      <Stack spacing={1.5}>
        {positions.map((p) => {
          const localAmount = p.localAmount ?? (p.lastPrice != null ? p.quantity * p.lastPrice : null);
          const sharePct = totalBase > 0 ? (p.marketValueBase / totalBase) * 100 : null;
          const priceLabel = p.isCash ? '—' : p.lastPrice != null ? `${fmt(p.lastPrice)} ${p.currencyId}` : '—';
          const averageLabel = p.isCash ? '—' : `${fmt(p.averageCost)} ${p.currencyId}`;

          return (
            <Paper key={p.instrumentId} variant="outlined" sx={{ p: 1.5, backgroundImage: 'none' }}>
              <Stack spacing={1.25}>
                <Stack direction="row" justifyContent="space-between" alignItems="flex-start" spacing={1}>
                  <Stack spacing={0.25}>
                    <Typography fontWeight={700}>{p.instrumentName || '—'}</Typography>
                    <Typography variant="caption" color="text.secondary">
                      {p.currencyId || '—'}
                    </Typography>
                  </Stack>
                  <Typography variant="body2" color="text.secondary">
                    {fmt(p.quantity)}
                  </Typography>
                </Stack>
                <Grid container spacing={1.25}>
                  <Grid item xs={6}>
                    <Typography variant="caption" color="text.secondary">
                      Цена
                    </Typography>
                    <Typography variant="body2">{priceLabel}</Typography>
                  </Grid>
                  <Grid item xs={6}>
                    <Typography variant="caption" color="text.secondary">
                      Сумма
                    </Typography>
                    <Typography variant="body2">{localAmount != null ? `${fmt(localAmount)} ${p.currencyId}` : '—'}</Typography>
                  </Grid>
                  <Grid item xs={6}>
                    <Typography variant="caption" color="text.secondary">
                      Доля, %
                    </Typography>
                    <Typography variant="body2">{fmtPct(sharePct)}</Typography>
                  </Grid>
                  <Grid item xs={6}>
                    <Typography variant="caption" color="text.secondary">
                      Средняя
                    </Typography>
                    <Typography variant="body2">{averageLabel}</Typography>
                  </Grid>
                  <Grid item xs={12}>
                    <Typography variant="caption" color="text.secondary">
                      Стоимость (base)
                    </Typography>
                    <Typography fontWeight={700}>{fmt(p.marketValueBase)}</Typography>
                  </Grid>
                </Grid>
              </Stack>
            </Paper>
          );
        })}
        {!!positions.length && (
          <Paper variant="outlined" sx={{ p: 1.5, backgroundImage: 'none' }}>
            <Stack spacing={1}>
              <Stack direction="row" justifyContent="space-between" alignItems="center">
                <Typography fontWeight={700}>Итого</Typography>
                <Typography fontWeight={700}>{fmt(totalBase)}</Typography>
              </Stack>
              {!!totalByCurrency.length && (
                <Stack spacing={0.25}>
                  <Typography variant="caption" color="text.secondary">
                    Сумма
                  </Typography>
                  {totalByCurrency.map((item) => (
                    <Typography key={item.currencyId} variant="body2">
                      {fmt(item.amount)} {item.currencyId}
                    </Typography>
                  ))}
                </Stack>
              )}
            </Stack>
          </Paper>
        )}
        {!positions.length && (
          <Paper variant="outlined" sx={{ p: 2, textAlign: 'center', backgroundImage: 'none' }}>
            <Typography color="text.secondary">Нет позиций</Typography>
          </Paper>
        )}
      </Stack>
    );
  }

  return (
    <TableContainer component={Paper} variant="outlined" sx={{ backgroundImage: 'none', borderRadius: 2 }}>
      <Table size="small" stickyHeader>
        <TableHead>
          <TableRow>
            <TableCell>Инструмент</TableCell>
            <TableCell align="right">Кол-во</TableCell>
            <TableCell align="right">Цена</TableCell>
            <TableCell align="right">Сумма</TableCell>
            <TableCell align="right">Доля, %</TableCell>
            <TableCell align="right">Средняя</TableCell>
            <TableCell align="right">Стоимость (base)</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {positions.map((p) => {
            const localAmount = p.localAmount ?? (p.lastPrice != null ? p.quantity * p.lastPrice : null);
            const sharePct = totalBase > 0 ? (p.marketValueBase / totalBase) * 100 : null;
            const priceLabel = p.isCash ? '—' : p.lastPrice != null ? `${fmt(p.lastPrice)} ${p.currencyId}` : '—';
            const averageLabel = p.isCash ? '—' : `${fmt(p.averageCost)} ${p.currencyId}`;

            return (
              <TableRow key={p.instrumentId} hover>
                <TableCell>
                  <Typography fontWeight={600}>{p.instrumentName || '—'}</Typography>
                  <Typography variant="caption" color="text.secondary">
                    {p.currencyId || '—'}
                  </Typography>
                </TableCell>
                <TableCell align="right">{fmt(p.quantity)}</TableCell>
                <TableCell align="right">{priceLabel}</TableCell>
                <TableCell align="right">{localAmount != null ? `${fmt(localAmount)} ${p.currencyId}` : '—'}</TableCell>
                <TableCell align="right">{fmtPct(sharePct)}</TableCell>
                <TableCell align="right">{averageLabel}</TableCell>
                <TableCell align="right">{fmt(p.marketValueBase)}</TableCell>
              </TableRow>
            );
          })}
          {!!positions.length && (
            <TableRow>
              <TableCell>
                <Typography fontWeight={700}>Итого</Typography>
              </TableCell>
              <TableCell align="right">—</TableCell>
              <TableCell align="right">—</TableCell>
              <TableCell align="right">
                {totalByCurrency.length ? (
                  <Stack spacing={0.25} alignItems="flex-end">
                    {totalByCurrency.map((item) => (
                      <Typography key={item.currencyId} variant="body2" fontWeight={600}>
                        {fmt(item.amount)} {item.currencyId}
                      </Typography>
                    ))}
                  </Stack>
                ) : (
                  '—'
                )}
              </TableCell>
              <TableCell align="right">{fmtPct(totalBase > 0 ? 100 : null)}</TableCell>
              <TableCell align="right">—</TableCell>
              <TableCell align="right">
                <Typography fontWeight={700}>{fmt(totalBase)}</Typography>
              </TableCell>
            </TableRow>
          )}
          {!positions.length && (
            <TableRow>
              <TableCell colSpan={7} align="center">
                <Typography color="text.secondary">Нет позиций</Typography>
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
