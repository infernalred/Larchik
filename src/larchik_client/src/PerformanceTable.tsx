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
import { PortfolioPerformance } from './types';

interface Props {
  items: PortfolioPerformance[];
}

const fmt = (v: number | null | undefined) =>
  v == null ? '—' : v.toLocaleString('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export function PerformanceTable({ items }: Props) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'));

  if (isMobile) {
    return (
      <Stack spacing={1.5}>
        {items.map((row) => (
          <Paper key={row.period} variant="outlined" sx={{ p: 1.5, backgroundImage: 'none' }}>
            <Stack spacing={1.25}>
              <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={1}>
                <Typography fontWeight={700}>{row.period}</Typography>
                <Typography color={row.returnPct >= 0 ? 'success.main' : 'error.main'} fontWeight={700}>
                  {row.returnPct == null ? '—' : `${(row.returnPct * 100).toFixed(2)}%`}
                </Typography>
              </Stack>
              <Grid container spacing={1.25}>
                <Grid item xs={6}>
                  <Typography variant="caption" color="text.secondary">
                    Начало NAV
                  </Typography>
                  <Typography variant="body2">{fmt(row.startNavBase)}</Typography>
                </Grid>
                <Grid item xs={6}>
                  <Typography variant="caption" color="text.secondary">
                    Конец NAV
                  </Typography>
                  <Typography variant="body2">{fmt(row.endNavBase)}</Typography>
                </Grid>
                <Grid item xs={6}>
                  <Typography variant="caption" color="text.secondary">
                    Потоки
                  </Typography>
                  <Typography variant="body2">{fmt(row.netInflowBase)}</Typography>
                </Grid>
                <Grid item xs={6}>
                  <Typography variant="caption" color="text.secondary">
                    P&L
                  </Typography>
                  <Typography variant="body2">{fmt(row.pnlBase)}</Typography>
                </Grid>
              </Grid>
            </Stack>
          </Paper>
        ))}
        {!items.length && (
          <Paper variant="outlined" sx={{ p: 2, textAlign: 'center', backgroundImage: 'none' }}>
            <Typography color="text.secondary">Нет данных</Typography>
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
