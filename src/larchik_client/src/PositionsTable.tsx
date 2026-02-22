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

export function PositionsTable({ positions }: Props) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'));

  if (isMobile) {
    return (
      <Stack spacing={1.5}>
        {positions.map((p) => (
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
                    Средняя
                  </Typography>
                  <Typography variant="body2">
                    {fmt(p.averageCost)} {p.currencyId}
                  </Typography>
                </Grid>
                <Grid item xs={6}>
                  <Typography variant="caption" color="text.secondary">
                    Последняя
                  </Typography>
                  <Typography variant="body2">
                    {p.lastPrice != null ? `${fmt(p.lastPrice)} ${p.currencyId}` : '—'}
                  </Typography>
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
        ))}
        {!positions.length && (
          <Paper variant="outlined" sx={{ p: 2, textAlign: 'center', backgroundImage: 'none' }}>
            <Typography color="text.secondary">Нет позиций</Typography>
          </Paper>
        )}
      </Stack>
    );
  }

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
