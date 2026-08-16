import { describe, expect, it } from 'vitest';
import { getDailyMoveUnavailableReason, isDailyMoveDisplayable } from './daily-attribution-domain';
import { buildDisplayPositions } from './portfolio-summary-domain';
import { PortfolioSummary, PositionDailyMove } from './types';

const dailyMove: PositionDailyMove = {
  startValueBase: 1_000,
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

describe('daily move in portfolio summary', () => {
  it('distinguishes a confirmed zero move from incomplete market data', () => {
    expect(isDailyMoveDisplayable(dailyMove)).toBe(true);
    expect(isDailyMoveDisplayable({ ...dailyMove, dataQuality: 'stale' })).toBe(false);
    expect(getDailyMoveUnavailableReason('stale')).toBe('Цена или курс старше даты отчёта');
  });

  it('uses daily moves already embedded into security and cash summary rows', () => {
    const summary: PortfolioSummary = {
      id: 'portfolio-id',
      name: 'Main',
      reportingCurrencyId: 'RUB',
      netInflowBase: 0,
      grossDepositsBase: 0,
      grossWithdrawalsBase: 0,
      cashBase: 9_500,
      positionsValueBase: 85_500,
      realizedBase: 0,
      unrealizedBase: 0,
      pnlBase: 0,
      navBase: 95_000,
      valuationMethod: 'adjustingAvg',
      dailyMove: null,
      cash: [{
        currencyId: 'USD',
        amount: 100,
        amountInBase: 9_500,
        dailyMove: { ...dailyMove, pnlBase: 500, fxEffectBase: 500 },
      }],
      positions: [{
        instrumentId: 'bond-id',
        instrumentName: 'Bond',
        instrumentType: 'Bond',
        currencyId: 'USD',
        quantity: 10,
        marketValueBase: 85_500,
        averageCost: 100,
        dailyMove: { ...dailyMove, pnlBase: -4_500, priceEffectBase: -9_000, fxEffectBase: 5_000 },
      }],
      realizedByInstrument: [],
    };

    const result = buildDisplayPositions(summary);

    expect(result[0].dailyMove).toMatchObject({ pnlBase: -4_500, priceEffectBase: -9_000, fxEffectBase: 5_000 });
    expect(result[1].dailyMove).toMatchObject({ pnlBase: 500, priceEffectBase: 0, fxEffectBase: 500 });
  });
});
