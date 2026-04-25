import { PositionHolding } from './types';

export type PurchaseMoveDirection = 'gain' | 'loss' | 'flat' | 'unknown';

export interface PurchaseMove {
  direction: PurchaseMoveDirection;
  absolute: number | null;
  percent: number | null;
  currencyId: string | null;
}

export function getPurchaseMove(position: PositionHolding): PurchaseMove {
  if (position.isCash || position.lastPrice == null || position.averageCost <= 0) {
    return {
      direction: 'unknown',
      absolute: null,
      percent: null,
      currencyId: null,
    };
  }

  const priceCurrencyId = position.priceCurrencyId ?? position.currencyId;
  const averageCurrencyId = position.averageCostCurrencyId ?? position.currencyId;
  const absolute = priceCurrencyId === averageCurrencyId ? position.lastPrice - position.averageCost : null;
  const percent = ((position.lastPrice - position.averageCost) / position.averageCost) * 100;
  const direction = percent > 0 ? 'gain' : percent < 0 ? 'loss' : 'flat';

  return {
    direction,
    absolute,
    percent,
    currencyId: absolute == null ? null : priceCurrencyId,
  };
}
