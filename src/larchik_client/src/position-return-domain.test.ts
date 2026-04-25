import { describe, expect, it } from 'vitest';
import { getPurchaseMove } from './position-return-domain';
import { PositionHolding } from './types';

const basePosition: PositionHolding = {
  instrumentId: 'asset-1',
  instrumentName: 'Asset',
  currencyId: 'USD',
  quantity: 10,
  lastPrice: 110,
  marketValueBase: 1100,
  averageCost: 100,
};

describe('position-return-domain', () => {
  it('marks a position above average cost as gain', () => {
    expect(getPurchaseMove(basePosition)).toEqual({
      direction: 'gain',
      absolute: 10,
      percent: 10,
      currencyId: 'USD',
    });
  });

  it('marks a position below average cost as loss', () => {
    expect(getPurchaseMove({ ...basePosition, lastPrice: 80 })).toEqual({
      direction: 'loss',
      absolute: -20,
      percent: -20,
      currencyId: 'USD',
    });
  });

  it('does not compare prices when currencies differ', () => {
    expect(getPurchaseMove({ ...basePosition, priceCurrencyId: 'USD', averageCostCurrencyId: 'RUB' })).toEqual({
      direction: 'gain',
      absolute: null,
      percent: 10,
      currencyId: null,
    });
  });

  it('returns unknown for cash and missing prices', () => {
    expect(getPurchaseMove({ ...basePosition, isCash: true })).toMatchObject({ direction: 'unknown' });
    expect(getPurchaseMove({ ...basePosition, lastPrice: undefined })).toMatchObject({ direction: 'unknown' });
  });
});
