import { PortfolioSummary, PositionHolding } from './types';

const CASH_LABELS: Record<string, string> = {
  RUB: 'Российский рубль',
  USD: 'Доллар США',
  EUR: 'Евро',
};

const POSITION_TYPE_ORDER: Record<string, number> = {
  Equity: 0,
  Bond: 1,
  Etf: 2,
  Currency: 3,
  Commodity: 4,
  Crypto: 5,
};

export function buildDisplayPositions(summary: PortfolioSummary): PositionHolding[] {
  const cashRows: PositionHolding[] = summary.cash.map((cash) => ({
    instrumentId: `cash:${cash.currencyId}`,
    instrumentName: CASH_LABELS[cash.currencyId] ?? cash.currencyId,
    instrumentType: 'Currency',
    categoryName: 'Деньги',
    currencyId: cash.currencyId,
    quantity: cash.amount,
    marketValueBase: cash.amountInBase,
    averageCost: 0,
    isCash: true,
    localAmount: cash.amount,
    dailyMove: cash.dailyMove ?? undefined,
  }));

  return [...summary.positions, ...cashRows].sort((left, right) => {
    const leftOrder = POSITION_TYPE_ORDER[left.instrumentType ?? ''] ?? 99;
    const rightOrder = POSITION_TYPE_ORDER[right.instrumentType ?? ''] ?? 99;
    if (leftOrder !== rightOrder) {
      return leftOrder - rightOrder;
    }

    return left.instrumentName.localeCompare(right.instrumentName, 'ru');
  });
}
