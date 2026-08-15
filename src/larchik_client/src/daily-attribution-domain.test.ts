import { describe, expect, it } from 'vitest';
import {
  attachDailyMoves,
  getDailyMoveUnavailableReason,
  isDailyMoveDisplayable,
  summarizeDailyAttributionWarnings,
} from './daily-attribution-domain';
import { DailyPnlAttribution, PositionDailyMove, PositionHolding } from './types';

const positions: PositionHolding[] = [
  {
    instrumentId: 'bond-id',
    instrumentName: 'Bond',
    instrumentType: 'Bond',
    currencyId: 'USD',
    quantity: 10,
    marketValueBase: 85_500,
    averageCost: 100,
  },
  {
    instrumentId: 'cash:USD',
    instrumentName: 'Доллар США',
    instrumentType: 'Currency',
    currencyId: 'USD',
    quantity: 100,
    marketValueBase: 9_500,
    averageCost: 0,
    isCash: true,
  },
];

const attribution: DailyPnlAttribution = {
  portfolioId: 'portfolio-id',
  name: 'Main',
  reportingCurrencyId: 'RUB',
  comparisonDate: '2026-08-10T00:00:00Z',
  valuationDate: '2026-08-11T00:00:00Z',
  startNavBase: 99_000,
  endNavBase: 95_000,
  externalFlowBase: 0,
  pnlBase: -4_000,
  returnPct: -0.0404,
  priceEffectBase: -9_000,
  securityFxEffectBase: 5_000,
  crossEffectBase: -500,
  tradingEffectBase: 0,
  cashFxEffectBase: 500,
  fxEffectBase: 5_500,
  incomeEffectBase: 0,
  feeEffectBase: 0,
  otherEffectBase: 0,
  reconciliationResidualBase: 0,
  isComplete: true,
  warnings: [],
  positions: [
    {
      instrumentId: 'bond-id',
      instrumentName: 'Bond',
      instrumentType: 'Bond',
      currencyId: 'USD',
      startQuantity: 10,
      endQuantity: 10,
      startPrice: 100,
      endPrice: 90,
      startPriceDate: '2026-08-10T00:00:00Z',
      endPriceDate: '2026-08-11T00:00:00Z',
      startFxRate: 90,
      endFxRate: 95,
      startFxRateDate: '2026-08-10T00:00:00Z',
      endFxRateDate: '2026-08-11T00:00:00Z',
      startMarketValueBase: 90_000,
      endMarketValueBase: 85_500,
      pnlBase: -4_500,
      returnPct: -0.05,
      priceReturnPct: -0.1,
      fxReturnPct: 0.0556,
      totalMarketReturnPct: -0.05,
      priceEffectBase: -9_000,
      fxEffectBase: 5_000,
      crossEffectBase: -500,
      tradingEffectBase: 0,
      incomeEffectBase: 0,
      feeEffectBase: 0,
      otherEffectBase: 0,
      dataQuality: 'complete',
      warnings: [],
    },
  ],
  cash: [
    {
      currencyId: 'USD',
      startAmount: 100,
      endAmount: 100,
      startFxRate: 90,
      endFxRate: 95,
      fxEffectBase: 500,
      dataQuality: 'complete',
    },
  ],
};

describe('attachDailyMoves', () => {
  it('attaches security and cash attribution to display rows', () => {
    const result = attachDailyMoves(positions, attribution);

    expect(result[0].dailyMove).toMatchObject({ pnlBase: -4_500, priceEffectBase: -9_000, fxEffectBase: 5_000 });
    expect(result[1].dailyMove).toMatchObject({ pnlBase: 500, priceEffectBase: 0, fxEffectBase: 500 });
  });

  it('summarizes repeated stale-price warnings instead of listing every instrument', () => {
    const staleAttribution: DailyPnlAttribution = {
      ...attribution,
      isComplete: false,
      valuationDate: '2026-08-14T00:00:00Z',
      warnings: [
        'Bond: конечная цена устарела: 2026-08-13',
        'Share: конечная цена устарела: 2026-08-13',
      ],
      positions: [
        {
          ...attribution.positions[0],
          endPriceDate: '2026-08-13T00:00:00Z',
          endFxRateDate: '2026-08-14T00:00:00Z',
          dataQuality: 'stale',
          warnings: ['конечная цена устарела: 2026-08-13'],
        },
        {
          ...attribution.positions[0],
          instrumentId: 'share-id',
          instrumentName: 'Share',
          endPriceDate: '2026-08-13T00:00:00Z',
          endFxRateDate: '2026-08-14T00:00:00Z',
          dataQuality: 'stale',
          warnings: ['конечная цена устарела: 2026-08-13'],
        },
      ],
    };

    expect(summarizeDailyAttributionWarnings(staleAttribution)).toEqual([
      'Цена старше даты отчёта у 2 бумаг.',
    ]);
  });

  it('distinguishes a confirmed zero move from incomplete market data', () => {
    const completeZero: PositionDailyMove = {
      pnlBase: 0,
      returnPct: 0,
      priceEffectBase: 0,
      fxEffectBase: 0,
      crossEffectBase: 0,
      tradingEffectBase: 0,
      incomeEffectBase: 0,
      feeEffectBase: 0,
      otherEffectBase: 0,
      dataQuality: 'complete',
    };

    expect(isDailyMoveDisplayable(completeZero)).toBe(true);
    expect(isDailyMoveDisplayable({ ...completeZero, dataQuality: 'stale' })).toBe(false);
    expect(getDailyMoveUnavailableReason('stale')).toBe('Цена или курс старше даты отчёта');
  });
});
