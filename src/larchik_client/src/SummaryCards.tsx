import InfoOutlined from '@mui/icons-material/InfoOutlined';
import { Box, Grid, Paper, Tooltip, Typography } from '@mui/material';
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
  description?: string;
  accent?: keyof typeof accents;
}

function StatCard({ title, value, currency, description, accent }: StatCardProps) {
  const content = (
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
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.75 }}>
        <Typography variant="body2" color="text.secondary">
          {title}
        </Typography>
        {description ? <InfoOutlined sx={{ fontSize: 16, color: 'text.secondary', opacity: 0.8 }} /> : null}
      </Box>
      <Typography component="div" variant="h6" sx={{ mt: 0.5, lineHeight: 1.25, fontWeight: 700 }}>
        {format(value)}{' '}
        <Box component="span" sx={{ color: 'text.secondary', fontSize: '0.875rem', fontWeight: 500 }}>
          {currency}
        </Box>
      </Typography>
    </Paper>
  );

  if (!description) {
    return content;
  }

  return (
    <Tooltip
      arrow
      placement="top"
      title={
        <Typography variant="body2" sx={{ whiteSpace: 'pre-line' }}>
          {description}
        </Typography>
      }
    >
      <Box sx={{ height: '100%' }}>{content}</Box>
    </Tooltip>
  );
}

interface Props {
  summary: PortfolioSummary;
}

export function SummaryCards({ summary }: Props) {
  const c = summary.reportingCurrencyId;
  const totalResultDescription =
    'Итоговый финансовый результат портфеля на текущий момент.\n' +
    'Считается как NAV - Net inflow.\n' +
    'Показывает, в плюсе вы или в минусе относительно чисто введённых денег.';

  return (
    <Grid container spacing={2}>
      <Grid size={{ xs: 12, sm: 6, md: 3 }}>
        <StatCard
          title="NAV"
          value={summary.navBase}
          currency={c}
          accent="nav"
          description={'Текущая общая стоимость портфеля.\nСчитается как Наличность + Позиции.\nЭто ориентир, сколько портфель стоит сейчас до комиссий продажи и налогов.'}
        />
      </Grid>
      <Grid size={{ xs: 12, sm: 6, md: 3 }}>
        <StatCard
          title="Наличность"
          value={summary.cashBase}
          currency={c}
          accent="cash"
          description={'Свободные деньги на счёте сейчас.\nСюда уже попадают покупки, продажи, дивиденды, купоны, комиссии и удержанные налоги как движение кэша.'}
        />
      </Grid>
      <Grid size={{ xs: 12, sm: 6, md: 3 }}>
        <StatCard
          title="Позиции"
          value={summary.positionsValueBase}
          currency={c}
          accent="positions"
          description={'Текущая рыночная стоимость открытых позиций по последним доступным ценам.\nЭто часть NAV без учёта свободного кэша.'}
        />
      </Grid>
      <Grid size={{ xs: 12, sm: 6, md: 3 }}>
        <StatCard
          title="Net inflow"
          value={summary.netInflowBase}
          currency={c}
          description={'Чистый внешний денежный поток в портфель.\nСчитается как Пополнения - Выводы.\nЭто сколько ваших денег сейчас суммарно заведено в портфель.'}
        />
      </Grid>
      <Grid size={{ xs: 12, sm: 6, md: 3 }}>
        <StatCard
          title="Пополнения"
          value={summary.grossDepositsBase}
          currency={c}
          description={'Все внешние вводы денег в портфель за всё время.\nСюда входят пополнения и входящие денежные переводы между счетами.'}
        />
      </Grid>
      <Grid size={{ xs: 12, sm: 6, md: 3 }}>
        <StatCard
          title="Выводы"
          value={summary.grossWithdrawalsBase}
          currency={c}
          description={'Все внешние выводы денег из портфеля за всё время.\nСюда входят выводы и исходящие денежные переводы между счетами.'}
        />
      </Grid>
      <Grid size={{ xs: 12, sm: 6, md: 3 }}>
        <StatCard
          title="Итоговый результат"
          value={summary.pnlBase}
          currency={c}
          accent={summary.pnlBase >= 0 ? 'cash' : 'unrealized'}
          description={totalResultDescription}
        />
      </Grid>
      <Grid size={{ xs: 12, sm: 6, md: 3 }}>
        <StatCard
          title="Реализовано"
          value={summary.realizedBase}
          currency={c}
          accent="realized"
          description={'Прибыль или убыток по уже закрытым сделкам.\nТорговые комиссии входят в расчёт реализованного результата.\nДивиденды, купоны и кэш-операции сюда не входят.'}
        />
      </Grid>
      <Grid size={{ xs: 12, sm: 6, md: 3 }}>
        <StatCard
          title="Нереализовано"
          value={summary.unrealizedBase}
          currency={c}
          accent="unrealized"
          description={'Прибыль или убыток по текущим открытым позициям.\nСчитается как текущая рыночная стоимость минус текущая себестоимость позиции.\nКомиссии на покупку входят в себестоимость и влияют на это значение.'}
        />
      </Grid>
    </Grid>
  );
}
