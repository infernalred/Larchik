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

const datePart = (value: string | null | undefined) => value?.slice(0, 10);

export function summarizeDailyAttributionWarnings(attribution: DailyPnlAttribution): string[] {
  const comparisonDate = datePart(attribution.comparisonDate);
  const valuationDate = datePart(attribution.valuationDate);
  const missingPriceCount = attribution.positions.filter(
    (position) => position.startPrice == null || position.endPrice == null,
  ).length;
  const stalePriceCount = attribution.positions.filter((position) => {
    const startPriceDate = datePart(position.startPriceDate);
    const endPriceDate = datePart(position.endPriceDate);
    return (startPriceDate != null && comparisonDate != null && startPriceDate < comparisonDate)
      || (endPriceDate != null && valuationDate != null && endPriceDate < valuationDate);
  }).length;
  const missingFxCount = attribution.positions.filter(
    (position) => position.startFxRate == null || position.endFxRate == null,
  ).length;
  const staleFxCount = attribution.positions.filter((position) => {
    const startFxDate = datePart(position.startFxRateDate);
    const endFxDate = datePart(position.endFxRateDate);
    return (startFxDate != null && comparisonDate != null && startFxDate < comparisonDate)
      || (endFxDate != null && valuationDate != null && endFxDate < valuationDate);
  }).length;
  const incompleteCashCount = attribution.cash.filter((cash) => cash.dataQuality !== 'complete').length;

  const messages: string[] = [];
  if (missingPriceCount > 0) messages.push(`Нет цены для ${missingPriceCount} бумаг.`);
  if (stalePriceCount > 0) messages.push(`Цена старше даты отчёта у ${stalePriceCount} бумаг.`);
  if (missingFxCount > 0) messages.push(`Нет валютного курса для ${missingFxCount} бумаг.`);
  if (staleFxCount > 0) messages.push(`Валютный курс старше даты отчёта у ${staleFxCount} бумаг.`);
  if (incompleteCashCount > 0) messages.push(`Неполные валютные данные по ${incompleteCashCount} денежным остаткам.`);

  const positionWarnings = new Set(
    attribution.positions.flatMap((position) =>
      position.warnings.map((warning) => `${position.instrumentName}: ${warning}`)),
  );
  const remainingWarnings = attribution.warnings.filter(
    (warning) => !positionWarnings.has(warning) && !warning.startsWith('Денежный остаток '),
  );

  return [...messages, ...remainingWarnings];
}
