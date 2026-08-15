import {
  Box,
  Chip,
  Grid,
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
  useMediaQuery,
  useTheme,
} from '@mui/material';
import ArrowDownwardRoundedIcon from '@mui/icons-material/ArrowDownwardRounded';
import ArrowUpwardRoundedIcon from '@mui/icons-material/ArrowUpwardRounded';
import RemoveRoundedIcon from '@mui/icons-material/RemoveRounded';
import WarningAmberRoundedIcon from '@mui/icons-material/WarningAmberRounded';
import { getDailyMoveUnavailableReason, isDailyMoveDisplayable } from './daily-attribution-domain';
import { getPurchaseMove, PurchaseMove } from './position-return-domain';
import { PositionHolding } from './types';

interface Props {
  positions: PositionHolding[];
  reportingCurrencyId?: string;
  dailyPnlBase?: number;
}

const fmt = (v: number | null | undefined) =>
  v == null ? '—' : v.toLocaleString('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

const fmtPct = (v: number | null | undefined) =>
  v == null ? '—' : `${v.toLocaleString('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}%`;

const fmtSigned = (v: number | null | undefined) => {
  if (v == null) return '—';
  const sign = v > 0 ? '+' : '';
  return `${sign}${fmt(v)}`;
};

const fmtSignedPct = (v: number | null | undefined) => {
  if (v == null) return '—';
  const sign = v > 0 ? '+' : '';
  return `${sign}${fmtPct(v)}`;
};

const getMoveTone = (move: PurchaseMove) => {
  if (move.direction === 'gain') {
    return {
      color: 'success.main',
      background: 'rgba(34, 197, 94, 0.12)',
      borderColor: 'rgba(34, 197, 94, 0.34)',
      label: 'Плюс',
      icon: <ArrowUpwardRoundedIcon fontSize="inherit" />,
    };
  }

  if (move.direction === 'loss') {
    return {
      color: 'error.main',
      background: 'rgba(239, 68, 68, 0.12)',
      borderColor: 'rgba(239, 68, 68, 0.34)',
      label: 'Минус',
      icon: <ArrowDownwardRoundedIcon fontSize="inherit" />,
    };
  }

  return {
    color: 'text.secondary',
    background: 'rgba(148, 163, 184, 0.1)',
    borderColor: 'rgba(148, 163, 184, 0.24)',
    label: move.direction === 'flat' ? 'В ноль' : 'Нет данных',
    icon: <RemoveRoundedIcon fontSize="inherit" />,
  };
};

function PurchaseMoveBadge({ move, dense = false }: { move: PurchaseMove; dense?: boolean }) {
  const tone = getMoveTone(move);
  const diffLabel = move.absolute == null || move.currencyId == null ? 'цены в разных валютах' : `${fmtSigned(move.absolute)} ${move.currencyId}`;

  if (move.direction === 'unknown') {
    return (
      <Typography variant={dense ? 'caption' : 'body2'} color="text.secondary">
        —
      </Typography>
    );
  }

  return (
    <Stack spacing={0.35} sx={{ alignItems: { xs: 'flex-start', sm: 'flex-end' } }}>
      <Chip
        size="small"
        icon={tone.icon}
        label={fmtSignedPct(move.percent)}
        sx={{
          minWidth: dense ? 76 : 84,
          justifyContent: 'center',
          color: tone.color,
          bgcolor: tone.background,
          border: '1px solid',
          borderColor: tone.borderColor,
          fontWeight: 800,
          '& .MuiChip-icon': {
            color: tone.color,
            ml: 0.75,
            mr: -0.25,
          },
        }}
      />
      <Typography variant="caption" color={tone.color} sx={{ fontWeight: 700, lineHeight: 1.15 }}>
        {tone.label} {diffLabel}
      </Typography>
    </Stack>
  );
}

function DailyMoveBadge({ position, reportingCurrencyId, dense = false }: { position: PositionHolding; reportingCurrencyId?: string; dense?: boolean }) {
  const move = position.dailyMove;
  if (!move) {
    return <Typography variant={dense ? 'caption' : 'body2'} color="text.secondary">—</Typography>;
  }

  if (!isDailyMoveDisplayable(move)) {
    return (
      <Tooltip title={getDailyMoveUnavailableReason(move.dataQuality)} arrow>
        <Stack direction="row" spacing={0.5} sx={{ justifyContent: { xs: 'flex-start', sm: 'flex-end' }, alignItems: 'center' }}>
          <Typography variant={dense ? 'caption' : 'body2'} color="text.secondary">—</Typography>
          <WarningAmberRoundedIcon sx={{ color: 'warning.main', fontSize: 16 }} />
        </Stack>
      </Tooltip>
    );
  }

  const color = move.pnlBase > 0 ? 'success.main' : move.pnlBase < 0 ? 'error.main' : 'text.secondary';
  const background = move.pnlBase > 0 ? 'rgba(34, 197, 94, 0.12)' : move.pnlBase < 0 ? 'rgba(239, 68, 68, 0.12)' : 'rgba(148, 163, 184, 0.1)';
  const borderColor = move.pnlBase > 0 ? 'rgba(34, 197, 94, 0.34)' : move.pnlBase < 0 ? 'rgba(239, 68, 68, 0.34)' : 'rgba(148, 163, 184, 0.24)';
  const details = position.isCash
    ? `валюта ${fmtSigned(move.fxEffectBase)}`
    : `бумага ${fmtSigned(move.priceEffectBase)} · FX ${fmtSigned(move.fxEffectBase + move.crossEffectBase)}`;

  return (
    <Stack spacing={0.35} sx={{ alignItems: { xs: 'flex-start', sm: 'flex-end' } }}>
      <Chip
        size="small"
        label={move.returnPct == null ? fmtSigned(move.pnlBase) : fmtSignedPct(move.returnPct * 100)}
        sx={{ color, bgcolor: background, border: '1px solid', borderColor, fontWeight: 800 }}
      />
      <Typography variant="caption" color={color} sx={{ fontWeight: 700, lineHeight: 1.15 }}>
        {fmtSigned(move.pnlBase)} {reportingCurrencyId ?? ''}
      </Typography>
      {!dense && (
        <Typography variant="caption" color="text.secondary" sx={{ lineHeight: 1.15 }}>
          {details}
        </Typography>
      )}
    </Stack>
  );
}

function buildCurrencyTotals(positions: PositionHolding[]) {
  const totals = new Map<string, number>();

  for (const position of positions) {
    const priceCurrency = position.priceCurrencyId ?? position.currencyId;
    const localAmount = position.localAmount ?? (position.lastPrice != null ? position.quantity * position.lastPrice : null);
    if (localAmount == null) {
      continue;
    }

    totals.set(priceCurrency, (totals.get(priceCurrency) ?? 0) + localAmount);
  }

  return [...totals.entries()].map(([currencyId, amount]) => ({
    currencyId,
    amount,
  }));
}

export function PositionsTable({ positions, reportingCurrencyId, dailyPnlBase }: Props) {
  const theme = useTheme();
  const useCardLayout = useMediaQuery(theme.breakpoints.down('xl'));
  const totalBase = positions.reduce((sum, p) => sum + p.marketValueBase, 0);
  const totalByCurrency = buildCurrencyTotals(positions);

  if (useCardLayout) {
    return (
      <Grid container spacing={1.5}>
        {positions.map((p) => {
          const priceCurrency = p.priceCurrencyId ?? p.currencyId;
          const averageCurrency = p.averageCostCurrencyId ?? p.currencyId;
          const localAmount = p.localAmount ?? (p.lastPrice != null ? p.quantity * p.lastPrice : null);
          const sharePct = totalBase > 0 ? (p.marketValueBase / totalBase) * 100 : null;
          const priceLabel = p.isCash ? '—' : p.lastPrice != null ? `${fmt(p.lastPrice)} ${priceCurrency}` : '—';
          const averageLabel = p.isCash ? '—' : `${fmt(p.averageCost)} ${averageCurrency}`;
          const purchaseMove = getPurchaseMove(p);
          const tone = getMoveTone(purchaseMove);

          return (
            <Grid key={p.instrumentId} size={{ xs: 12, md: 6, lg: 12 }}>
              <Paper
                variant="outlined"
                sx={{
                  p: 1.5,
                  height: '100%',
                  overflow: 'hidden',
                  backgroundImage: 'none',
                  borderColor: purchaseMove.direction === 'unknown' ? 'rgba(148, 163, 184, 0.22)' : tone.borderColor,
                }}
              >
                <Stack spacing={1.25}>
                  <Stack direction="row" spacing={1.25} sx={{ justifyContent: 'space-between', alignItems: 'flex-start' }}>
                    <Box sx={{ minWidth: 0 }}>
                      <Typography sx={{ fontWeight: 800, lineHeight: 1.25, overflowWrap: 'anywhere' }}>{p.instrumentName || '—'}</Typography>
                      <Typography variant="caption" color="text.secondary">
                        {p.currencyId || '—'} · {fmt(p.quantity)} шт.
                      </Typography>
                    </Box>
                    <Box sx={{ flexShrink: 0 }}>
                      <Stack spacing={0.75} sx={{ alignItems: 'flex-end' }}>
                        <DailyMoveBadge position={p} reportingCurrencyId={reportingCurrencyId} dense />
                        <PurchaseMoveBadge move={purchaseMove} dense />
                      </Stack>
                    </Box>
                  </Stack>

                  <Grid container spacing={1}>
                    <Grid size={6}>
                      <Typography variant="caption" color="text.secondary">
                        Цена
                      </Typography>
                      <Typography variant="body2">{priceLabel}</Typography>
                    </Grid>
                    <Grid size={6}>
                      <Typography variant="caption" color="text.secondary">
                        Средняя
                      </Typography>
                      <Typography variant="body2">{averageLabel}</Typography>
                    </Grid>
                    <Grid size={6}>
                      <Typography variant="caption" color="text.secondary">
                        Сумма
                      </Typography>
                      <Typography variant="body2">{localAmount != null ? `${fmt(localAmount)} ${priceCurrency}` : '—'}</Typography>
                    </Grid>
                    <Grid size={6}>
                      <Typography variant="caption" color="text.secondary">
                        Доля
                      </Typography>
                      <Typography variant="body2">{fmtPct(sharePct)}</Typography>
                    </Grid>
                  </Grid>

                  <Box
                    sx={{
                      display: 'flex',
                      justifyContent: 'space-between',
                      alignItems: 'center',
                      gap: 1,
                      pt: 1,
                      borderTop: '1px solid rgba(148, 163, 184, 0.16)',
                    }}
                  >
                    <Typography variant="caption" color="text.secondary">
                      Стоимость
                    </Typography>
                    <Typography sx={{ fontWeight: 800 }}>{fmt(p.marketValueBase)}</Typography>
                  </Box>
                </Stack>
              </Paper>
            </Grid>
          );
        })}
        {!!positions.length && (
          <Grid size={12}>
            <Paper variant="outlined" sx={{ p: 1.5, backgroundImage: 'none' }}>
              <Stack spacing={1}>
                <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center' }}>
                  <Typography sx={{ fontWeight: 700 }}>Итого</Typography>
                  <Typography sx={{ fontWeight: 700 }}>{fmt(totalBase)}</Typography>
                </Stack>
                {dailyPnlBase != null && (
                  <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'center' }}>
                    <Typography variant="caption" color="text.secondary">За день</Typography>
                    <Typography sx={{ fontWeight: 700, color: dailyPnlBase > 0 ? 'success.main' : dailyPnlBase < 0 ? 'error.main' : 'text.secondary' }}>
                      {fmtSigned(dailyPnlBase)} {reportingCurrencyId ?? ''}
                    </Typography>
                  </Stack>
                )}
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
          </Grid>
        )}
        {!positions.length && (
          <Grid size={12}>
            <Paper variant="outlined" sx={{ p: 2, textAlign: 'center', backgroundImage: 'none' }}>
              <Typography color="text.secondary">Нет позиций</Typography>
            </Paper>
          </Grid>
        )}
      </Grid>
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
            <TableCell align="right">За день</TableCell>
            <TableCell align="right">От покупки</TableCell>
            <TableCell align="right">Стоимость (base)</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {positions.map((p) => {
            const priceCurrency = p.priceCurrencyId ?? p.currencyId;
            const averageCurrency = p.averageCostCurrencyId ?? p.currencyId;
            const localAmount = p.localAmount ?? (p.lastPrice != null ? p.quantity * p.lastPrice : null);
            const sharePct = totalBase > 0 ? (p.marketValueBase / totalBase) * 100 : null;
            const priceLabel = p.isCash ? '—' : p.lastPrice != null ? `${fmt(p.lastPrice)} ${priceCurrency}` : '—';
            const averageLabel = p.isCash ? '—' : `${fmt(p.averageCost)} ${averageCurrency}`;
            const purchaseMove = getPurchaseMove(p);

            return (
              <TableRow key={p.instrumentId} hover>
                <TableCell>
                  <Typography sx={{ fontWeight: 600 }}>{p.instrumentName || '—'}</Typography>
                  <Typography variant="caption" color="text.secondary">
                    {p.currencyId || '—'}
                  </Typography>
                </TableCell>
                <TableCell align="right">{fmt(p.quantity)}</TableCell>
                <TableCell align="right">{priceLabel}</TableCell>
                <TableCell align="right">{localAmount != null ? `${fmt(localAmount)} ${priceCurrency}` : '—'}</TableCell>
                <TableCell align="right">{fmtPct(sharePct)}</TableCell>
                <TableCell align="right">{averageLabel}</TableCell>
                <TableCell align="right">
                  <DailyMoveBadge position={p} reportingCurrencyId={reportingCurrencyId} />
                </TableCell>
                <TableCell align="right">
                  <PurchaseMoveBadge move={purchaseMove} />
                </TableCell>
                <TableCell align="right">{fmt(p.marketValueBase)}</TableCell>
              </TableRow>
            );
          })}
          {!!positions.length && (
            <TableRow>
              <TableCell>
                <Typography sx={{ fontWeight: 700 }}>Итого</Typography>
              </TableCell>
              <TableCell align="right">—</TableCell>
              <TableCell align="right">—</TableCell>
              <TableCell align="right">—</TableCell>
              <TableCell align="right">{fmtPct(totalBase > 0 ? 100 : null)}</TableCell>
              <TableCell align="right">—</TableCell>
              <TableCell align="right">
                <Typography sx={{ fontWeight: 700, color: dailyPnlBase == null ? 'text.secondary' : dailyPnlBase > 0 ? 'success.main' : dailyPnlBase < 0 ? 'error.main' : 'text.secondary' }}>
                  {dailyPnlBase == null ? '—' : `${fmtSigned(dailyPnlBase)} ${reportingCurrencyId ?? ''}`}
                </Typography>
              </TableCell>
              <TableCell align="right">—</TableCell>
              <TableCell align="right">
                <Typography sx={{ fontWeight: 700 }}>{fmt(totalBase)}</Typography>
              </TableCell>
            </TableRow>
          )}
          {!positions.length && (
            <TableRow>
              <TableCell colSpan={9} align="center">
                <Typography color="text.secondary">Нет позиций</Typography>
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
