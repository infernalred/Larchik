import { Category, Currency, Instrument, InstrumentModel, InstrumentType, PriceSource } from './types';

export const INSTRUMENT_TYPE_OPTIONS: { value: InstrumentType; label: string }[] = [
  { value: 'Equity', label: 'Акция' },
  { value: 'Bond', label: 'Облигация' },
  { value: 'Etf', label: 'ETF' },
  { value: 'Currency', label: 'Валюта' },
  { value: 'Commodity', label: 'Товар' },
  { value: 'Crypto', label: 'Крипто' },
];

export const INSTRUMENT_TYPE_LABELS: Record<InstrumentType, string> = Object.fromEntries(
  INSTRUMENT_TYPE_OPTIONS.map(({ value, label }) => [value, label]),
) as Record<InstrumentType, string>;

export const PRICE_SOURCE_OPTIONS: { value: PriceSource; label: string }[] = [
  { value: 'MOEX', label: 'MOEX' },
  { value: 'TBANK', label: 'T-Bank' },
];

export const PRICE_SOURCE_LABELS: Record<PriceSource, string> = Object.fromEntries(
  PRICE_SOURCE_OPTIONS.map(({ value, label }) => [value, label]),
) as Record<PriceSource, string>;

export function requiresInstrumentIsin(type: InstrumentType): boolean {
  return type === 'Equity' || type === 'Bond' || type === 'Etf';
}

export function createInstrumentEditorInitialModel(
  initial?: Instrument | null,
  categories: Category[] = [],
  currencies: Currency[] = [],
): InstrumentModel {
  return {
    name: initial?.name ?? '',
    ticker: initial?.ticker ?? '',
    isin: initial?.isin ?? '',
    figi: initial?.figi ?? '',
    type: initial?.type ?? 'Equity',
    currencyId: initial?.currencyId ?? currencies[0]?.id ?? 'USD',
    categoryId: initial?.categoryId ?? categories[0]?.id ?? 0,
    exchange: initial?.exchange ?? '',
    country: initial?.country ?? '',
    isTrading: initial?.isTrading ?? true,
    priceSource: initial?.priceSource ?? null,
  };
}

export function normalizeInstrumentEditorModel(form: InstrumentModel): InstrumentModel {
  const normalized: InstrumentModel = {
    name: form.name.trim(),
    ticker: form.ticker.trim().toUpperCase(),
    isin: form.isin?.trim() ? form.isin.trim().toUpperCase() : undefined,
    figi: form.figi?.trim() ? form.figi.trim().toUpperCase() : undefined,
    type: form.type,
    currencyId: form.currencyId.trim().toUpperCase(),
    categoryId: form.categoryId,
    exchange: form.exchange?.trim() ? form.exchange.trim().toUpperCase() : undefined,
    country: form.country?.trim() ? form.country.trim().toUpperCase() : undefined,
    isTrading: form.isTrading,
    priceSource: form.isTrading ? form.priceSource ?? null : null,
  };

  return {
    ...normalized,
  };
}
