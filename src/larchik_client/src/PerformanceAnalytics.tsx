import { useEffect, useMemo, useState } from 'react';
import { Box, ButtonBase, Divider, Grid, Paper, Stack, Typography } from '@mui/material';
import { PortfolioPerformance, PortfolioSummary } from './types';

interface Props {
  summary: PortfolioSummary | null;
  items: PortfolioPerformance[];
  currency: string;
}

type AnalyticsTab = 'assets' | 'companies' | 'industries' | 'currency' | 'portfolio';
type PortfolioRange = '3m' | '6m' | '1y' | 'all';

interface AnalyticsPoint {
  period: string;
  date: Date;
  longLabel: string;
  shortLabel: string;
  returnPct: number;
  endNavBase: number;
  pnlBase: number;
  netInflowBase: number;
}

interface SliceEntry {
  label: string;
  value: number;
  share: number;
  color: string;
}

interface CompositionSection {
  slices: SliceEntry[];
  count: number;
}

const LONG_MONTH_FORMATTER = new Intl.DateTimeFormat('ru-RU', { month: 'long', year: 'numeric' });
const SHORT_MONTH_FORMATTER = new Intl.DateTimeFormat('ru-RU', { month: 'short' });
const MONTH_ONLY_FORMATTER = new Intl.DateTimeFormat('ru-RU', { month: 'long' });
const YEAR_FORMATTER = new Intl.DateTimeFormat('ru-RU', { year: 'numeric' });

const TAB_LABELS: Record<AnalyticsTab, string> = {
  assets: 'Активы',
  companies: 'Компании',
  industries: 'Отрасли',
  currency: 'Валюта',
  portfolio: 'Портфель',
};

const RANGE_LABELS: Record<PortfolioRange, string> = {
  '3m': '3 мес.',
  '6m': 'Полгода',
  '1y': 'Год',
  all: 'За все время',
};

const RANGE_LIMITS: Record<PortfolioRange, number> = {
  '3m': 3,
  '6m': 6,
  '1y': 12,
  all: Number.POSITIVE_INFINITY,
};

const DONUT_COLORS = ['#52c0f5', '#b76ad7', '#9fd06e', '#ffc947', '#ff72a7', '#6aa1dc', '#f49a45', '#4ba59d', '#ee7b53', '#73b478'];

const ASSET_TYPE_LABELS: Record<string, string> = {
  Equity: 'Акции',
  Bond: 'Облигации',
  Etf: 'Фонды',
  Commodity: 'Металлы и сырье',
  Crypto: 'Криптовалюта',
  Currency: 'Валюта и кэш',
};

const formatMoney = (value: number) =>
  value.toLocaleString('ru-RU', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });

const formatPercent = (value: number | null | undefined) => {
  if (value == null || Number.isNaN(value)) {
    return '—';
  }

  return `${(value * 100).toFixed(2)}%`;
};

const formatShare = (value: number) =>
  `${value.toLocaleString('ru-RU', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}%`;

function toPeriodDate(period: string) {
  const [year, month] = period.split('-').map(Number);
  return new Date(Date.UTC(year, (month || 1) - 1, 1));
}

function capitalize(value: string) {
  return value ? value[0].toUpperCase() + value.slice(1) : value;
}

function buildPoints(items: PortfolioPerformance[]): AnalyticsPoint[] {
  return [...items]
    .sort((left, right) => left.period.localeCompare(right.period))
    .map((item) => {
      const date = toPeriodDate(item.period);
      return {
        period: item.period,
        date,
        longLabel: capitalize(LONG_MONTH_FORMATTER.format(date)),
        shortLabel: SHORT_MONTH_FORMATTER.format(date).replace('.', ''),
        returnPct: item.returnPct ?? 0,
        endNavBase: item.endNavBase,
        pnlBase: item.pnlBase,
        netInflowBase: item.netInflowBase,
      };
    });
}

function accumulateGroup(target: Map<string, number>, key: string, value: number) {
  target.set(key, (target.get(key) ?? 0) + value);
}

function toSlices(groups: Map<string, number>, maxItems = 8): SliceEntry[] {
  const sorted = [...groups.entries()]
    .filter(([, value]) => value > 0)
    .sort((left, right) => right[1] - left[1]);

  if (!sorted.length) {
    return [];
  }

  const top = sorted.slice(0, maxItems);
  const otherValue = sorted.slice(maxItems).reduce((sum, [, value]) => sum + value, 0);
  const total = sorted.reduce((sum, [, value]) => sum + value, 0);
  const entries = otherValue > 0 ? [...top, ['Другие', otherValue] as const] : top;

  return entries.map(([label, value], index) => ({
    label,
    value,
    share: total > 0 ? value / total : 0,
    color: DONUT_COLORS[index % DONUT_COLORS.length],
  }));
}

function toSection(groups: Map<string, number>, maxItems = 8): CompositionSection {
  return {
    slices: toSlices(groups, maxItems),
    count: [...groups.entries()].filter(([, value]) => value > 0).length,
  };
}

function describeCount(count: number, one: string, two: string, five: string) {
  const mod10 = count % 10;
  const mod100 = count % 100;
  if (mod10 === 1 && mod100 !== 11) return `${count} ${one}`;
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return `${count} ${two}`;
  return `${count} ${five}`;
}

function buildComposition(summary: PortfolioSummary) {
  const assets = new Map<string, number>();
  const companies = new Map<string, number>();
  const industries = new Map<string, number>();
  const currency = new Map<string, number>();

  for (const position of summary.positions) {
    const assetLabel = ASSET_TYPE_LABELS[position.instrumentType ?? ''] ?? 'Другое';
    accumulateGroup(assets, assetLabel, position.marketValueBase);
    accumulateGroup(companies, position.instrumentName, position.marketValueBase);
    accumulateGroup(industries, position.categoryName ?? 'Без отрасли', position.marketValueBase);
    accumulateGroup(currency, position.currencyId, position.marketValueBase);
  }

  if (summary.cashBase > 0) {
    accumulateGroup(assets, ASSET_TYPE_LABELS.Currency, summary.cashBase);
  }

  for (const cash of summary.cash) {
    accumulateGroup(currency, cash.currencyId, cash.amountInBase);
  }

  return {
    assets: toSection(assets, 8),
    companies: toSection(companies, 8),
    industries: toSection(industries, 8),
    currency: toSection(currency, 8),
  };
}

function buildConicGradient(slices: SliceEntry[]) {
  if (!slices.length) {
    return 'conic-gradient(#334155 0% 100%)';
  }

  let current = 0;
  const segments = slices.map((slice) => {
    const start = current * 100;
    current += slice.share;
    const end = current * 100;
    return `${slice.color} ${start}% ${end}%`;
  });

  return `conic-gradient(${segments.join(', ')})`;
}

function CompositionDonut({
  slices,
  total,
  centerLabel,
  currency,
}: {
  slices: SliceEntry[];
  total: number;
  centerLabel: string;
  currency: string;
}) {
  return (
    <Box sx={{ display: 'grid', placeItems: 'center', py: { xs: 1, md: 2 } }}>
      <Box
        sx={{
          width: { xs: 250, md: 310 },
          height: { xs: 250, md: 310 },
          borderRadius: '50%',
          display: 'grid',
          placeItems: 'center',
          background: buildConicGradient(slices),
          boxShadow: '0 24px 60px rgba(15,23,42,0.24)',
        }}
      >
        <Box
          sx={{
            width: '74%',
            height: '74%',
            borderRadius: '50%',
            bgcolor: 'rgba(15,26,48,0.98)',
            border: '1px solid rgba(148,163,184,0.18)',
            display: 'grid',
            placeItems: 'center',
            textAlign: 'center',
            px: 2,
          }}
        >
          <Box>
            <Typography variant="h5" sx={{ fontWeight: 800 }}>
              {formatMoney(total)} {currency}
            </Typography>
            <Typography color="text.secondary">{centerLabel}</Typography>
          </Box>
        </Box>
      </Box>
    </Box>
  );
}

function CompositionView({
  title,
  subtitle,
  slices,
  count,
  currency,
  entityLabel,
}: {
  title: string;
  subtitle: string;
  slices: SliceEntry[];
  count: number;
  currency: string;
  entityLabel: { one: string; two: string; five: string };
}) {
  if (!slices.length) {
    return (
      <Paper variant="outlined" sx={{ p: { xs: 2, md: 3 }, backgroundImage: 'none' }}>
        <Typography variant="h6" sx={{ mb: 0.5, fontWeight: 700 }}>
          {title}
        </Typography>
        <Typography color="text.secondary">{subtitle}</Typography>
      </Paper>
    );
  }

  const total = slices.reduce((sum, slice) => sum + slice.value, 0);
  const centerLabel = describeCount(count, entityLabel.one, entityLabel.two, entityLabel.five);

  return (
    <Paper variant="outlined" sx={{ p: { xs: 2, md: 3 }, backgroundImage: 'none' }}>
      <Grid container spacing={{ xs: 2, md: 3 }} sx={{ alignItems: 'center' }}>
        <Grid size={{ xs: 12, md: 6, lg: 7 }}>
          <Stack spacing={1.5}>
            <Box>
              <Typography variant="h6" sx={{ mb: 0.5, fontWeight: 700 }}>
                {title}
              </Typography>
              <Typography color="text.secondary">{subtitle}</Typography>
            </Box>

            <Stack spacing={1}>
              {slices.map((slice) => (
                <Stack key={slice.label} direction="row" spacing={1.5} sx={{ justifyContent: 'space-between', alignItems: 'center' }}>
                  <Stack direction="row" spacing={1.25} sx={{ minWidth: 0, alignItems: 'center' }}>
                    <Box sx={{ width: 9, height: 9, borderRadius: '50%', bgcolor: slice.color, flexShrink: 0 }} />
                    <Typography sx={{ minWidth: 0 }} noWrap>
                      {slice.label}
                    </Typography>
                  </Stack>
                  <Typography sx={{ whiteSpace: 'nowrap', fontWeight: 700 }}>
                    {formatShare(slice.share * 100)}
                  </Typography>
                </Stack>
              ))}
            </Stack>
          </Stack>
        </Grid>

        <Grid size={{ xs: 12, md: 6, lg: 5 }}>
          <CompositionDonut slices={slices} total={total} centerLabel={centerLabel} currency={currency} />
        </Grid>
      </Grid>
    </Paper>
  );
}

function PortfolioValueChart({
  points,
  currency,
  range,
}: {
  points: AnalyticsPoint[];
  currency: string;
  range: PortfolioRange;
}) {
  const defaultSelectedPeriod = points[points.length - 1]?.period ?? points[0]?.period ?? '';
  const [selectedPeriod, setSelectedPeriod] = useState(defaultSelectedPeriod);

  useEffect(() => {
    setSelectedPeriod(defaultSelectedPeriod);
  }, [defaultSelectedPeriod]);

  if (!points.length) {
    return (
      <Paper variant="outlined" sx={{ p: 2, textAlign: 'center', backgroundImage: 'none' }}>
        <Typography color="text.secondary">Недостаточно данных для графика стоимости портфеля.</Typography>
      </Paper>
    );
  }

  const width = 760;
  const height = 300;
  const isAllTime = range === 'all';
  const padding = { top: isAllTime ? 42 : 18, right: 18, bottom: isAllTime ? 18 : 44, left: 18 };
  const innerWidth = width - padding.left - padding.right;
  const innerHeight = height - padding.top - padding.bottom;
  const values = points.map((point) => point.endNavBase);
  const minValue = Math.min(...values);
  const maxValue = Math.max(...values);
  const baseValue = minValue > 0 ? minValue * 0.92 : 0;
  const valueRange = Math.max(maxValue - baseValue, 1);
  const step = innerWidth / Math.max(points.length, 1);
  const barWidth = Math.max(16, Math.min(26, step * 0.56));
  const labelStep = points.length > 8 ? Math.ceil(points.length / 6) : 1;
  const selectedPoint = points.find((point) => point.period === selectedPeriod) ?? points[points.length - 1];
  const selectedIndex = Math.max(
    points.findIndex((point) => point.period === selectedPoint.period),
    0,
  );
  const yearMarkers = isAllTime
    ? points
        .map((point, index) => ({ point, index }))
        .filter(({ point, index }) => index === 0 || point.date.getUTCFullYear() !== points[index - 1].date.getUTCFullYear())
    : [];

  const selectedBarHeight = ((selectedPoint.endNavBase - baseValue) / valueRange) * innerHeight;
  const selectedX = padding.left + step * selectedIndex + (step - barWidth) / 2;
  const selectedY = padding.top + innerHeight - selectedBarHeight;
  const tooltipWidth = 168;
  const tooltipHeight = 62;
  const tooltipX = Math.min(
    Math.max(selectedX + barWidth / 2 - tooltipWidth / 2, padding.left),
    width - padding.right - tooltipWidth,
  );
  const tooltipY = Math.max(selectedY - tooltipHeight - 10, padding.top + 6);
  const tooltipMonth = MONTH_ONLY_FORMATTER.format(selectedPoint.date);
  const tooltipYear = YEAR_FORMATTER.format(selectedPoint.date);

  return (
    <Box component="svg" viewBox={`0 0 ${width} ${height}`} sx={{ width: '100%', height: { xs: 250, md: 300 }, display: 'block' }}>
      {yearMarkers.map(({ point, index }) => {
        const x = padding.left + step * index;
        return (
          <g key={`year-${point.period}`}>
            <line
              x1={x}
              x2={x}
              y1={padding.top}
              y2={padding.top + innerHeight}
              stroke="rgba(148,163,184,0.22)"
              strokeDasharray="4 5"
            />
            <text
              x={Math.min(x + 4, width - padding.right - 24)}
              y={padding.top - 10}
              fill="rgba(148,163,184,0.78)"
              fontSize="12"
              textAnchor="start"
            >
              {YEAR_FORMATTER.format(point.date)}
            </text>
          </g>
        );
      })}

      {Array.from({ length: 4 }).map((_, index) => {
        const y = padding.top + (innerHeight / 3) * index;
        const value = maxValue - ((maxValue - baseValue) / 3) * index;
        return (
          <g key={index}>
            <line
              x1={padding.left}
              x2={width - padding.right}
              y1={y}
              y2={y}
              stroke={index === 3 ? 'rgba(148,163,184,0.28)' : 'rgba(148,163,184,0.15)'}
              strokeDasharray={isAllTime ? '4 5' : undefined}
            />
            <text x={width - padding.right} y={y - 6} fill="rgba(148,163,184,0.88)" fontSize="12" textAnchor="end">
              {`${formatMoney(value)} ${currency}`}
            </text>
          </g>
        );
      })}

      {points.map((point, index) => {
        const barHeight = ((point.endNavBase - baseValue) / valueRange) * innerHeight;
        const x = padding.left + step * index + (step - barWidth) / 2;
        const y = padding.top + innerHeight - barHeight;
        const isLast = index === points.length - 1;
        const isSelected = point.period === selectedPoint.period;
        return (
          <g key={point.period}>
            <rect
              x={x}
              y={y}
              width={barWidth}
              height={Math.max(barHeight, 3)}
              rx={Math.min(barWidth / 2, 8)}
              fill={isSelected ? '#7db8ff' : '#65a8e8'}
              opacity={isSelected || isLast ? 1 : 0.92}
              style={{ cursor: 'pointer' }}
              onClick={() => setSelectedPeriod(point.period)}
            />
            {!isAllTime && (index % labelStep === 0 || isLast) && (
              <text
                x={x + barWidth / 2}
                y={height - 16}
                fill={isSelected || isLast ? '#f8fafc' : 'rgba(148,163,184,0.9)'}
                fontSize="12"
                fontWeight={isSelected || isLast ? 700 : 500}
                textAnchor="middle"
              >
                {point.shortLabel}
              </text>
            )}
          </g>
        );
      })}

      <g>
        <line
          x1={selectedX + barWidth / 2}
          x2={selectedX + barWidth / 2}
          y1={padding.top}
          y2={padding.top + innerHeight}
          stroke="rgba(255,255,255,0.18)"
          strokeDasharray="4 4"
        />
        <rect
          x={tooltipX}
          y={tooltipY}
          width={tooltipWidth}
          height={tooltipHeight}
          rx={16}
          fill="#f8fafc"
        />
        <text x={tooltipX + 16} y={tooltipY + 24} fill="#111827" fontSize="13" fontWeight={500}>
          {isAllTime ? `${tooltipMonth} ${tooltipYear}` : tooltipMonth}
        </text>
        <text x={tooltipX + 16} y={tooltipY + 47} fill="#020617" fontSize="15" fontWeight={800}>
          {`${formatMoney(selectedPoint.endNavBase)} ${currency}`}
        </text>
      </g>
    </Box>
  );
}

function ReturnChart({ points }: { points: AnalyticsPoint[] }) {
  if (!points.length) {
    return (
      <Paper variant="outlined" sx={{ p: 2, textAlign: 'center', backgroundImage: 'none' }}>
        <Typography color="text.secondary">Недостаточно данных для помесячной доходности.</Typography>
      </Paper>
    );
  }

  const width = 760;
  const height = 300;
  const padding = { top: 28, right: 24, bottom: 46, left: 24 };
  const innerWidth = width - padding.left - padding.right;
  const innerHeight = height - padding.top - padding.bottom;
  const maxAbsValue = Math.max(...points.map((point) => Math.abs(point.returnPct)), 0.01);
  const halfHeight = innerHeight / 2;
  const step = innerWidth / Math.max(points.length, 1);
  const barWidth = Math.max(12, Math.min(24, step * 0.56));
  const zeroY = padding.top + halfHeight;
  const tickValues = [maxAbsValue, maxAbsValue / 2, 0, -maxAbsValue / 2, -maxAbsValue];
  const labelStep = points.length > 8 ? Math.ceil(points.length / 6) : 1;

  return (
    <Box component="svg" viewBox={`0 0 ${width} ${height}`} sx={{ width: '100%', height: { xs: 250, md: 300 }, display: 'block' }}>
      <defs>
        <linearGradient id="returnPositive" x1="0%" x2="0%" y1="0%" y2="100%">
          <stop offset="0%" stopColor="#34d399" />
          <stop offset="100%" stopColor="#0f766e" />
        </linearGradient>
        <linearGradient id="returnNegative" x1="0%" x2="0%" y1="0%" y2="100%">
          <stop offset="0%" stopColor="#fb7185" />
          <stop offset="100%" stopColor="#b91c1c" />
        </linearGradient>
      </defs>

      {tickValues.map((tick) => {
        const y = zeroY - (tick / maxAbsValue) * halfHeight;
        return (
          <g key={tick}>
            <line x1={padding.left} x2={width - padding.right} y1={y} y2={y} stroke="rgba(148,163,184,0.16)" />
            <text x={width - padding.right} y={y - 6} fill="rgba(148,163,184,0.92)" fontSize="12" textAnchor="end">
              {formatPercent(tick)}
            </text>
          </g>
        );
      })}

      <line x1={padding.left} x2={width - padding.right} y1={zeroY} y2={zeroY} stroke="rgba(226,232,240,0.52)" />

      {points.map((point, index) => {
        const x = padding.left + step * index + (step - barWidth) / 2;
        const barHeight = (Math.abs(point.returnPct) / maxAbsValue) * halfHeight;
        const y = point.returnPct >= 0 ? zeroY - barHeight : zeroY;
        const fill = point.returnPct >= 0 ? 'url(#returnPositive)' : 'url(#returnNegative)';
        const isLast = index === points.length - 1;
        return (
          <g key={point.period}>
            <rect x={x} y={y} width={barWidth} height={Math.max(barHeight, 2)} rx={barWidth / 2} fill={fill} opacity={0.96} />
            {(index % labelStep === 0 || isLast) && (
              <text
                x={x + barWidth / 2}
                y={height - 16}
                fill={isLast ? '#f8fafc' : 'rgba(148,163,184,0.9)'}
                fontSize="12"
                fontWeight={isLast ? 700 : 500}
                textAnchor="middle"
              >
                {point.shortLabel}
              </text>
            )}
          </g>
        );
      })}
    </Box>
  );
}

function PortfolioView({
  summary,
  points,
  currency,
}: {
  summary: PortfolioSummary;
  points: AnalyticsPoint[];
  currency: string;
}) {
  const [range, setRange] = useState<PortfolioRange>('all');
  const visiblePoints = points.slice(-RANGE_LIMITS[range]);
  const totalReturn = summary.navBase - summary.netInflowBase;

  return (
    <Stack spacing={{ xs: 2, md: 3 }}>
      <Grid container spacing={{ xs: 2, md: 3 }}>
        <Grid size={{ xs: 12, lg: 8 }}>
          <Paper variant="outlined" sx={{ p: { xs: 2, md: 2.5 }, backgroundImage: 'none' }}>
            <Stack spacing={1.25}>
              <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5} sx={{ justifyContent: 'space-between' }}>
                <Box>
                  <Typography variant="h6" sx={{ fontWeight: 700 }}>
                    Стоимость портфеля
                  </Typography>
                  <Typography color="text.secondary">Динамика NAV по месяцам в стиле брокерской аналитики.</Typography>
                </Box>
                <Stack direction="row" spacing={0.75} useFlexGap sx={{ flexWrap: 'wrap' }}>
                  {(Object.keys(RANGE_LABELS) as PortfolioRange[]).map((item) => (
                    <ButtonBase
                      key={item}
                      onClick={() => setRange(item)}
                      sx={{
                        px: 1.25,
                        py: 0.75,
                        borderRadius: 999,
                        border: '1px solid',
                        borderColor: item === range ? 'primary.main' : 'divider',
                        color: item === range ? 'text.primary' : 'text.secondary',
                        backgroundColor: item === range ? 'rgba(82,192,245,0.12)' : 'transparent',
                      }}
                    >
                      <Typography variant="body2" sx={{ fontWeight: item === range ? 700 : 500 }}>
                        {RANGE_LABELS[item]}
                      </Typography>
                    </ButtonBase>
                  ))}
                </Stack>
              </Stack>
              <PortfolioValueChart points={visiblePoints} currency={currency} range={range} />
            </Stack>
          </Paper>
        </Grid>

        <Grid size={{ xs: 12, lg: 4 }}>
          <Paper variant="outlined" sx={{ p: { xs: 2, md: 2.5 }, backgroundImage: 'none', height: '100%' }}>
            <Typography variant="h6" sx={{ mb: 1.5, fontWeight: 700 }}>
              Статистика движения средств
            </Typography>
            <Stack divider={<Divider flexItem />} spacing={0}>
              {[
                { label: 'Доходность', value: totalReturn, color: totalReturn >= 0 ? 'success.main' : 'error.main' },
                { label: 'Чистый поток', value: summary.netInflowBase },
                { label: 'Пополнения', value: summary.grossDepositsBase },
                { label: 'Выводы', value: summary.grossWithdrawalsBase },
                { label: 'Позиции', value: summary.positionsValueBase },
                { label: 'Наличность', value: summary.cashBase },
                { label: 'Реализовано', value: summary.realizedBase },
                { label: 'Нереализовано', value: summary.unrealizedBase },
              ].map((item) => (
                <Stack key={item.label} direction="row" sx={{ py: 1.25, justifyContent: 'space-between', alignItems: 'center' }}>
                  <Typography color="text.secondary">{item.label}</Typography>
                  <Typography color={item.color} sx={{ fontWeight: 700 }}>
                    {item.value == null ? '—' : `${formatMoney(item.value)} ${currency}`}
                  </Typography>
                </Stack>
              ))}
            </Stack>
          </Paper>
        </Grid>
      </Grid>

      <Paper variant="outlined" sx={{ p: { xs: 2, md: 2.5 }, backgroundImage: 'none' }}>
        <Typography variant="h6" sx={{ mb: 0.5, fontWeight: 700 }}>
          Помесячная доходность
        </Typography>
        <Typography color="text.secondary" sx={{ mb: 2 }}>
          Отдельный график по месяцам, чтобы не читать сухую таблицу.
        </Typography>
        <ReturnChart points={visiblePoints} />
      </Paper>
    </Stack>
  );
}

export function PerformanceAnalytics({ summary, items, currency }: Props) {
  const [tab, setTab] = useState<AnalyticsTab>('assets');
  const points = useMemo(() => buildPoints(items), [items]);
  const composition = useMemo(() => (summary ? buildComposition(summary) : null), [summary]);

  if (!summary) {
    return (
      <Paper variant="outlined" sx={{ p: { xs: 2, md: 3 }, backgroundImage: 'none' }}>
        <Typography variant="h6" sx={{ mb: 0.75, fontWeight: 700 }}>
          Аналитика
        </Typography>
        <Typography color="text.secondary">Не удалось загрузить структуру портфеля для брокерской аналитики.</Typography>
      </Paper>
    );
  }

  return (
    <Stack spacing={{ xs: 2, md: 3 }}>
      <Paper
        variant="outlined"
        sx={{
          p: { xs: 2, md: 3 },
          background:
            'linear-gradient(140deg, rgba(255,255,255,0.03) 0%, rgba(82,192,245,0.08) 45%, rgba(183,106,215,0.08) 100%)',
        }}
      >
        <Stack spacing={2}>
          <Box>
            <Typography variant="overline" color="text.secondary">
              Аналитика как у брокера
            </Typography>
            <Typography variant="h5" sx={{ mb: 0.5, fontWeight: 800 }}>
              Структура и динамика портфеля
            </Typography>
            <Typography color="text.secondary">
              Разбивка по активам, компаниям, отраслям и валютам плюс отдельный блок по самому портфелю.
            </Typography>
          </Box>

          <Stack direction="row" useFlexGap spacing={0} sx={{ flexWrap: 'wrap' }}>
            <Box
              sx={{
                display: 'inline-flex',
                border: '1px solid',
                borderColor: 'divider',
                borderRadius: 2.5,
                overflow: 'hidden',
                flexWrap: 'wrap',
              }}
            >
              {(Object.keys(TAB_LABELS) as AnalyticsTab[]).map((item) => (
                <ButtonBase
                  key={item}
                  onClick={() => setTab(item)}
                  sx={{
                    px: { xs: 2, md: 3 },
                    py: 1.5,
                    borderRight: item === 'portfolio' ? 'none' : '1px solid rgba(148,163,184,0.18)',
                    borderColor: 'divider',
                    backgroundColor: tab === item ? 'rgba(255,214,10,0.16)' : 'transparent',
                    boxShadow: tab === item ? 'inset 0 0 0 1px #ffd60a' : 'none',
                  }}
                >
                  <Typography sx={{ fontWeight: tab === item ? 700 : 500 }}>{TAB_LABELS[item]}</Typography>
                </ButtonBase>
              ))}
            </Box>
          </Stack>
        </Stack>
      </Paper>

      {tab === 'assets' && composition && (
        <CompositionView
          title="Структура по активам"
          subtitle="Показывает, во что именно сейчас распределен портфель."
          slices={composition.assets.slices}
          count={composition.assets.count}
          currency={currency}
          entityLabel={{ one: 'актив', two: 'актива', five: 'активов' }}
        />
      )}

      {tab === 'companies' && composition && (
        <CompositionView
          title="Структура по компаниям"
          subtitle="Крупнейшие позиции и их вес в портфеле."
          slices={composition.companies.slices}
          count={composition.companies.count}
          currency={currency}
          entityLabel={{ one: 'компания', two: 'компании', five: 'компаний' }}
        />
      )}

      {tab === 'industries' && composition && (
        <CompositionView
          title="Структура по отраслям"
          subtitle="Разбивка по категориям инструментов из справочника."
          slices={composition.industries.slices}
          count={composition.industries.count}
          currency={currency}
          entityLabel={{ one: 'отрасль', two: 'отрасли', five: 'отраслей' }}
        />
      )}

      {tab === 'currency' && composition && (
        <CompositionView
          title="Структура по валютам"
          subtitle="Учитывает и сами позиции, и денежный остаток."
          slices={composition.currency.slices}
          count={composition.currency.count}
          currency={currency}
          entityLabel={{ one: 'валюта', two: 'валюты', five: 'валют' }}
        />
      )}

      {tab === 'portfolio' && <PortfolioView summary={summary} points={points} currency={currency} />}
    </Stack>
  );
}
