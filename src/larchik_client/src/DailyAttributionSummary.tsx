import { Alert, Box, Chip, Grid, Paper, Stack, Tooltip, Typography } from '@mui/material';
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import { DailyPnlAttribution } from './types';

interface Props {
  attribution: DailyPnlAttribution;
}

const fmt = (value: number) =>
  value.toLocaleString('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

const fmtSigned = (value: number) => `${value > 0 ? '+' : ''}${fmt(value)}`;

const fmtPct = (value: number | null) =>
  value == null
    ? '—'
    : `${value > 0 ? '+' : ''}${(value * 100).toLocaleString('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}%`;

const fmtDate = (value: string) =>
  new Date(value).toLocaleDateString('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric', timeZone: 'UTC' });

function EffectCard({ label, value, currency, hint }: { label: string; value: number; currency: string; hint: string }) {
  const color = value > 0 ? 'success.main' : value < 0 ? 'error.main' : 'text.secondary';
  return (
    <Paper variant="outlined" sx={{ p: 1.25, height: '100%', backgroundImage: 'none' }}>
      <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
        <Typography variant="caption" color="text.secondary">{label}</Typography>
        <Tooltip title={hint} arrow>
          <InfoOutlinedIcon sx={{ color: 'text.secondary', fontSize: 14 }} />
        </Tooltip>
      </Stack>
      <Typography sx={{ mt: 0.25, color, fontWeight: 800 }}>
        {fmtSigned(value)} {currency}
      </Typography>
    </Paper>
  );
}

export function DailyAttributionSummary({ attribution }: Props) {
  const currency = attribution.reportingCurrencyId;
  const totalColor = attribution.pnlBase > 0 ? 'success.main' : attribution.pnlBase < 0 ? 'error.main' : 'text.primary';
  const fxEffect = attribution.securityFxEffectBase + attribution.cashFxEffectBase;

  return (
    <Paper
      variant="outlined"
      sx={{
        p: { xs: 1.5, sm: 2 },
        borderRadius: 2.5,
        background: 'linear-gradient(135deg, rgba(15,118,110,0.14), rgba(14,165,233,0.06))',
      }}
    >
      <Stack spacing={1.5}>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ justifyContent: 'space-between', alignItems: { sm: 'center' } }}>
          <Box>
            <Typography variant="overline" color="text.secondary">Результат за торговый день</Typography>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'baseline', flexWrap: 'wrap' }}>
              <Typography variant="h5" sx={{ color: totalColor, fontWeight: 900 }}>
                {fmtSigned(attribution.pnlBase)} {currency}
              </Typography>
              <Typography sx={{ color: totalColor, fontWeight: 800 }}>{fmtPct(attribution.returnPct)}</Typography>
            </Stack>
            <Typography variant="caption" color="text.secondary">
              Закрытие {fmtDate(attribution.comparisonDate)} → {fmtDate(attribution.valuationDate)} · внешние потоки исключены
            </Typography>
          </Box>
          <Chip
            size="small"
            color={attribution.isComplete ? 'success' : 'warning'}
            variant="outlined"
            label={attribution.isComplete ? 'Данные полные' : 'Есть неполные данные'}
          />
        </Stack>

        <Grid container spacing={1}>
          <Grid size={{ xs: 6, md: 3 }}>
            <EffectCard label="Цена бумаг" value={attribution.priceEffectBase} currency={currency} hint="Движение биржевых цен при неизменном начальном валютном курсе." />
          </Grid>
          <Grid size={{ xs: 6, md: 3 }}>
            <EffectCard label="Валюта" value={fxEffect} currency={currency} hint="Переоценка валютных бумаг и денежных остатков из-за изменения курсов." />
          </Grid>
          <Grid size={{ xs: 6, md: 3 }}>
            <EffectCard label="Выплаты" value={attribution.incomeEffectBase} currency={currency} hint="Дивиденды и купоны, зачисленные в течение периода." />
          </Grid>
          <Grid size={{ xs: 6, md: 3 }}>
            <EffectCard label="Комиссии" value={attribution.feeEffectBase} currency={currency} hint="Брокерские комиссии и отдельные комиссионные операции." />
          </Grid>
        </Grid>

        {(attribution.tradingEffectBase !== 0 || attribution.crossEffectBase !== 0 || attribution.otherEffectBase !== 0) && (
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}>
            {attribution.tradingEffectBase !== 0 && <Chip size="small" label={`Сделки: ${fmtSigned(attribution.tradingEffectBase)} ${currency}`} />}
            {attribution.crossEffectBase !== 0 && <Chip size="small" label={`Цена × FX: ${fmtSigned(attribution.crossEffectBase)} ${currency}`} />}
            {attribution.otherEffectBase !== 0 && <Chip size="small" label={`Другое: ${fmtSigned(attribution.otherEffectBase)} ${currency}`} />}
          </Stack>
        )}

        {!attribution.isComplete && attribution.warnings.length > 0 && (
          <Alert severity="warning" variant="outlined">
            {attribution.warnings.slice(0, 3).join(' · ')}
            {attribution.warnings.length > 3 ? ` · ещё ${attribution.warnings.length - 3}` : ''}
          </Alert>
        )}
      </Stack>
    </Paper>
  );
}
