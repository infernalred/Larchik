import { DailyPnlAttribution, PositionDailyMove, PositionHolding } from './types';

export function attachDailyMoves(
  positions: PositionHolding[],
  attribution: DailyPnlAttribution | null,
): PositionHolding[] {
  if (!attribution) return positions;

  const positionMoves = new Map(attribution.positions.map((item) => [item.instrumentId, item]));
  const cashMoves = new Map(attribution.cash.map((item) => [item.currencyId.toUpperCase(), item]));

  return positions.map((position) => {
    if (position.isCash) {
      const cash = cashMoves.get(position.currencyId.toUpperCase());
      if (!cash) return position;
      const startValue = cash.startAmount * (cash.startFxRate ?? 1);
      const move: PositionDailyMove = {
        pnlBase: cash.fxEffectBase,
        returnPct: startValue === 0 ? null : cash.fxEffectBase / startValue,
        priceEffectBase: 0,
        fxEffectBase: cash.fxEffectBase,
        crossEffectBase: 0,
        tradingEffectBase: 0,
        incomeEffectBase: 0,
        feeEffectBase: 0,
        otherEffectBase: 0,
        dataQuality: cash.dataQuality,
      };
      return { ...position, dailyMove: move };
    }

    const item = positionMoves.get(position.instrumentId);
    if (!item) return position;
    return {
      ...position,
      dailyMove: {
        pnlBase: item.pnlBase,
        returnPct: item.returnPct,
        priceEffectBase: item.priceEffectBase,
        fxEffectBase: item.fxEffectBase,
        crossEffectBase: item.crossEffectBase,
        tradingEffectBase: item.tradingEffectBase,
        incomeEffectBase: item.incomeEffectBase,
        feeEffectBase: item.feeEffectBase,
        otherEffectBase: item.otherEffectBase,
        dataQuality: item.dataQuality,
      },
    };
  });
}
