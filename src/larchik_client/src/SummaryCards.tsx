import { Box, Grid, Paper, Typography } from '@mui/material';
import { PortfolioSummary } from './types';

const accents: Record<string, string> = {
  nav: 'linear-gradient(140deg, rgba(14,165,233,0.26) 0%, rgba(34,211,238,0.1) 100%)',
  cash: 'linear-gradient(140deg, rgba(34,197,94,0.26) 0%, rgba(74,222,128,0.1) 100%)',
  positions: 'linear-gradient(140deg, rgba(15,118,110,0.3) 0%, rgba(45,212,191,0.08) 100%)',
  realized: 'linear-gradient(140deg, rgba(245,158,11,0.28) 0%, rgba(251,191,36,0.12) 100%)',
  unrealized: 'linear-gradient(140deg, rgba(239,68,68,0.26) 0%, rgba(248,113,113,0.12) 100%)',
};

const format = (v: number | null | undefined) =>
  v == null ? '—' : v.toLocaleString('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

interface StatCardProps {
  title: string;
  value: number | null | undefined;
  currency: string;
  accent?: keyof typeof accents;
}

function StatCard({ title, value, currency, accent }: StatCardProps) {
  return (
    <Paper
      elevation={0}
      sx={{
        p: { xs: 1.5, sm: 2 },
        borderRadius: 2.5,
        minHeight: { xs: 'auto', sm: 112 },
        background: accent ? accents[accent] : 'rgba(255,255,255,0.04)',
        border: '1px solid rgba(148, 163, 184, 0.2)',
      }}
    >
      <Typography variant="body2" color="text.secondary">
        {title}
      </Typography>
      <Typography component="div" variant="h6" fontWeight={700} sx={{ mt: 0.5, lineHeight: 1.25 }}>
        {format(value)}{' '}
        <Box component="span" sx={{ color: 'text.secondary', fontSize: '0.875rem', fontWeight: 500 }}>
          {currency}
        </Box>
      </Typography>
    </Paper>
  );
}

interface Props {
  summary: PortfolioSummary;
}

export function SummaryCards({ summary }: Props) {
  const c = summary.reportingCurrencyId;
  return (
    <Grid container spacing={2}>
      <Grid item xs={12} sm={6} md={4}>
        <StatCard title="NAV" value={summary.navBase} currency={c} accent="nav" />
      </Grid>
      <Grid item xs={12} sm={6} md={4}>
        <StatCard title="Наличность" value={summary.cashBase} currency={c} accent="cash" />
      </Grid>
      <Grid item xs={12} sm={6} md={4}>
        <StatCard title="Позиции" value={summary.positionsValueBase} currency={c} accent="positions" />
      </Grid>
      <Grid item xs={12} sm={6} md={4}>
        <StatCard title="Реализовано" value={summary.realizedBase} currency={c} accent="realized" />
      </Grid>
      <Grid item xs={12} sm={6} md={4}>
        <StatCard title="Нереализовано" value={summary.unrealizedBase} currency={c} accent="unrealized" />
      </Grid>
      <Grid item xs={12} sm={6} md={4}>
        <StatCard title="Net inflow" value={summary.netInflowBase} currency={c} />
      </Grid>
    </Grid>
  );
}
