import { describe, expect, it } from 'vitest';
import { createPortfolioInitialForm, normalizePortfolioForm } from './portfolio-domain';

describe('portfolio-domain', () => {
  it('creates initial form from available currencies', () => {
    expect(createPortfolioInitialForm([{ id: 'EUR' }, { id: 'USD' }])).toEqual({
      name: '',
      brokerId: '',
      reportingCurrencyId: 'EUR',
    });
  });

  it('falls back to RUB when no currencies are loaded', () => {
    expect(createPortfolioInitialForm()).toEqual({
      name: '',
      brokerId: '',
      reportingCurrencyId: 'RUB',
    });
  });

  it('normalizes portfolio form before submit', () => {
    expect(
      normalizePortfolioForm({
        name: '  Main account  ',
        brokerId: 'broker-1',
        reportingCurrencyId: ' usd ',
      }),
    ).toEqual({
      name: 'Main account',
      brokerId: 'broker-1',
      reportingCurrencyId: 'USD',
    });
  });
});
