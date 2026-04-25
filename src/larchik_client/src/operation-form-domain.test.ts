import { describe, expect, it } from 'vitest';
import { normalizeOperationFormModel, requiresOperationInstrument, validateOperationFormModel } from './operation-form-domain';
import { OperationModel } from './types';

const createModel = (overrides: Partial<OperationModel> = {}): OperationModel => ({
  instrumentId: undefined,
  type: 'Buy',
  quantity: 1,
  price: 10,
  fee: 0,
  currencyId: 'RUB',
  tradeDate: '2026-04-25',
  settlementDate: undefined,
  note: undefined,
  ...overrides,
});

describe('operation-form-domain', () => {
  it('requires instrument for security operations', () => {
    expect(requiresOperationInstrument('Buy')).toBe(true);
    expect(requiresOperationInstrument('Sell')).toBe(true);
    expect(requiresOperationInstrument('Dividend')).toBe(true);
    expect(requiresOperationInstrument('Deposit')).toBe(false);
  });

  it('validates missing instrument before submitting security operations', () => {
    expect(validateOperationFormModel(createModel({ type: 'Buy', instrumentId: undefined }))).toBe(
      'Выберите инструмент для операции с бумагой.',
    );
  });

  it('removes stale instrument from cash operation payloads', () => {
    const model = normalizeOperationFormModel(createModel({ type: 'Deposit', instrumentId: 'instrument-id' }));

    expect(model.instrumentId).toBeUndefined();
  });
});
