import { describe, expect, it } from 'vitest';
import { createQuickDepositInitialState, normalizeQuickDepositState, validateQuickDepositAmount } from './quick-deposit-domain';

describe('quick-deposit-domain', () => {
  it('builds initial state from preferred or first available currency', () => {
    expect(createQuickDepositInitialState('USD', [{ id: 'RUB' }, { id: 'USD' }])).toEqual({
      amount: 100000,
      currency: 'USD',
      note: 'Ввод средств',
    });

    expect(createQuickDepositInitialState(undefined, [{ id: 'EUR' }])).toEqual({
      amount: 100000,
      currency: 'EUR',
      note: 'Ввод средств',
    });
  });

  it('falls back to rub when currency list is empty', () => {
    expect(createQuickDepositInitialState(undefined, [])).toEqual({
      amount: 100000,
      currency: 'RUB',
      note: 'Ввод средств',
    });
  });

  it('normalizes submit payload', () => {
    expect(
      normalizeQuickDepositState({
        amount: 1250.5,
        currency: ' usd ',
        note: '  Пополнение  ',
      }),
    ).toEqual({
      amount: 1250.5,
      currency: 'USD',
      note: 'Пополнение',
    });
  });

  it('validates positive amount', () => {
    expect(validateQuickDepositAmount(0)).toBe('Введите сумму больше нуля');
    expect(validateQuickDepositAmount(-10)).toBe('Введите сумму больше нуля');
    expect(validateQuickDepositAmount(10)).toBeNull();
  });
});
