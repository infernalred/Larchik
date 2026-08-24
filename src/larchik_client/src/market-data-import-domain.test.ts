import { describe, expect, it } from 'vitest';
import {
  createMarketDataImportForm,
  isTerminalMarketDataImportStatus,
  normalizeMarketDataImportForm,
  validateMarketDataImportForm,
} from './market-data-import-domain';

describe('market-data-import-domain', () => {
  it('creates a safe form with today as the default start date', () => {
    expect(createMarketDataImportForm('2026-08-24')).toEqual({
      source: 'MOEX',
      isin: '',
      fromDate: '2026-08-24',
    });
  });

  it('normalizes ISIN before sending it to the API', () => {
    expect(normalizeMarketDataImportForm({
      source: 'TBANK',
      isin: ' ru000a107t19 ',
      fromDate: '2024-01-01',
    })).toEqual({
      source: 'TBANK',
      isin: 'RU000A107T19',
      fromDate: '2024-01-01',
    });
  });

  it('validates ISIN checksum and date', () => {
    expect(validateMarketDataImportForm({
      source: 'MOEX',
      isin: 'RU000A107T19',
      fromDate: '2024-01-01',
    }, '2026-08-24')).toBeNull();

    expect(validateMarketDataImportForm({
      source: 'MOEX',
      isin: 'RU000A107T18',
      fromDate: '2024-01-01',
    }, '2026-08-24')).toBe('Проверьте ISIN: формат или контрольная цифра неверны.');

    expect(validateMarketDataImportForm({
      source: 'MOEX',
      isin: 'RU000A107T19',
      fromDate: '2026-08-25',
    }, '2026-08-24')).toBe('Дата начала не может быть в будущем.');
  });

  it('recognizes terminal statuses used to stop polling', () => {
    expect(isTerminalMarketDataImportStatus('Queued')).toBe(false);
    expect(isTerminalMarketDataImportStatus('LoadingPrices')).toBe(false);
    expect(isTerminalMarketDataImportStatus('Succeeded')).toBe(true);
    expect(isTerminalMarketDataImportStatus('SkippedExisting')).toBe(true);
    expect(isTerminalMarketDataImportStatus('Failed')).toBe(true);
  });
});
