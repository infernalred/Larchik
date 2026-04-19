export interface PortfolioFormModel {
  name: string;
  brokerId: string;
  reportingCurrencyId: string;
}

const DEFAULT_REPORTING_CURRENCY = 'RUB';

export function createPortfolioInitialForm(currencies: ReadonlyArray<{ id: string }> = []): PortfolioFormModel {
  return {
    name: '',
    brokerId: '',
    reportingCurrencyId: currencies[0]?.id ?? DEFAULT_REPORTING_CURRENCY,
  };
}

export function normalizePortfolioForm(form: PortfolioFormModel): PortfolioFormModel {
  return {
    name: form.name.trim(),
    brokerId: form.brokerId,
    reportingCurrencyId: form.reportingCurrencyId.trim().toUpperCase(),
  };
}
