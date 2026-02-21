import { Grid, Paper, Typography } from '@mui/material';
import { PortfolioSummary } from './types';

const accents: Record<string, string> = {
  nav: 'linear-gradient(120deg, #0ea5e9 0%, #22d3ee 100%)',
  cash: 'linear-gradient(120deg, #22c55e 0%, #86efac 100%)',
  positions: 'linear-gradient(120deg, #6366f1 0%, #a855f7 100%)',
  realized: 'linear-gradient(120deg, #f59e0b 0%, #fbbf24 100%)',
  unrealized: 'linear-gradient(120deg, #ef4444 0%, #fb7185 100%)',
};

const format = (v: number) => v.toLocaleString('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

interface StatCardProps {
  title: string;
  value: number;
  currency: string;
  accent?: keyof typeof accents;
}

function StatCard({ title, value, currency, accent }: StatCardProps) {
  return (
    <Paper
      elevation={0}
      sx={{
        p: 2,
        borderRadius: 2,
        background: accent ? accents[accent] : 'rgba(255,255,255,0.04)',
        color: accent ? '#0b1224' : 'inherit',
      }}
    >
      <Typography variant="body2" color={accent ? 'inherit' : 'text.secondary'}>
        {title}
      </Typography>
      <Typography variant="h6" fontWeight={700}>
        {format(value)} <Typography component="span">{currency}</Typography>
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
