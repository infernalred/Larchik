import { MarketDataImportModel, MarketDataImportStatus } from './types';

const DATE_ONLY_REGEX = /^(\d{4})-(\d{2})-(\d{2})$/;

export const MARKET_DATA_IMPORT_STATUS_LABELS: Record<MarketDataImportStatus, string> = {
  Queued: 'В очереди',
  ResolvingInstrument: 'Ищем инструмент',
  LoadingPrices: 'Загружаем цены',
  Succeeded: 'Завершено',
  SkippedExisting: 'Инструмент уже существует',
  Failed: 'Ошибка',
};

export function createMarketDataImportForm(today: string): MarketDataImportModel {
  return {
    source: 'MOEX',
    isin: '',
    fromDate: today,
  };
}

export function normalizeMarketDataImportForm(form: MarketDataImportModel): MarketDataImportModel {
  return {
    ...form,
    isin: form.isin.trim().toUpperCase(),
  };
}

export function validateMarketDataImportForm(form: MarketDataImportModel, today: string): string | null {
  const normalized = normalizeMarketDataImportForm(form);
  if (!isValidIsin(normalized.isin)) {
    return 'Проверьте ISIN: формат или контрольная цифра неверны.';
  }

  if (!isValidDateOnly(normalized.fromDate)) {
    return 'Укажите корректную дату начала.';
  }

  if (normalized.fromDate > today) {
    return 'Дата начала не может быть в будущем.';
  }

  return null;
}

export function isTerminalMarketDataImportStatus(status: MarketDataImportStatus): boolean {
  return status === 'Succeeded' || status === 'SkippedExisting' || status === 'Failed';
}

function isValidIsin(value: string): boolean {
  if (!/^[A-Z0-9]{12}$/.test(value)) {
    return false;
  }

  const expanded = [...value]
    .map((character) => /[A-Z]/.test(character) ? String(character.charCodeAt(0) - 55) : character)
    .join('');
  let sum = 0;
  let doubleDigit = false;
  for (let index = expanded.length - 1; index >= 0; index -= 1) {
    let digit = Number(expanded[index]);
    if (doubleDigit) {
      digit *= 2;
      digit = Math.floor(digit / 10) + digit % 10;
    }

    sum += digit;
    doubleDigit = !doubleDigit;
  }

  return sum % 10 === 0;
}

function isValidDateOnly(value: string): boolean {
  const match = DATE_ONLY_REGEX.exec(value);
  if (!match) {
    return false;
  }

  const [, yearValue, monthValue, dayValue] = match;
  const year = Number(yearValue);
  const month = Number(monthValue);
  const day = Number(dayValue);
  const date = new Date(Date.UTC(year, month - 1, day));
  return date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day;
}
