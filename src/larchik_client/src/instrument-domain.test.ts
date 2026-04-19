import { describe, expect, it } from 'vitest';
import { createInstrumentEditorInitialModel, normalizeInstrumentEditorModel, requiresInstrumentIsin } from './instrument-domain';
import { Category, Currency, InstrumentModel } from './types';

describe('instrument-domain', () => {
  it('requires ISIN only for tradable security types', () => {
    expect(requiresInstrumentIsin('Equity')).toBe(true);
    expect(requiresInstrumentIsin('Bond')).toBe(true);
    expect(requiresInstrumentIsin('Etf')).toBe(true);
    expect(requiresInstrumentIsin('Currency')).toBe(false);
    expect(requiresInstrumentIsin('Commodity')).toBe(false);
    expect(requiresInstrumentIsin('Crypto')).toBe(false);
  });

  it('builds editor initial model from current instrument or first available reference data', () => {
    const categories: Category[] = [{ id: 7, name: 'Недвижимость' }];
    const currencies: Currency[] = [{ id: 'EUR', name: 'Euro' }];

    expect(createInstrumentEditorInitialModel(null, categories, currencies)).toEqual<InstrumentModel>({
      name: '',
      ticker: '',
      isin: '',
      figi: '',
      type: 'Equity',
      currencyId: 'EUR',
      categoryId: 7,
      exchange: '',
      country: '',
      isTrading: true,
      priceSource: null,
    });
  });

  it('normalizes instrument editor payload before submit', () => {
    const input: InstrumentModel = {
      name: '  Test bond  ',
      ticker: '  ru000a10elf6 ',
      isin: '  ru000a10elf6 ',
      figi: '  tcs00a10elf6 ',
      type: 'Bond',
      currencyId: ' rub ',
      categoryId: 14,
      exchange: ' tqcb ',
      country: ' ru ',
      isTrading: false,
      priceSource: 'MOEX',
    };

    expect(normalizeInstrumentEditorModel(input)).toEqual<InstrumentModel>({
      name: 'Test bond',
      ticker: 'RU000A10ELF6',
      isin: 'RU000A10ELF6',
      figi: 'TCS00A10ELF6',
      type: 'Bond',
      currencyId: 'RUB',
      categoryId: 14,
      exchange: 'tqcb',
      country: 'ru',
      isTrading: false,
      priceSource: null,
    });
  });
});
