export interface QuickDepositState {
  amount: number;
  currency: string;
  note: string;
}

const DEFAULT_AMOUNT = 100000;
const DEFAULT_CURRENCY = 'RUB';
const DEFAULT_NOTE = 'Ввод средств';

export function createQuickDepositInitialState(
  preferredCurrencyId?: string,
  currencies: ReadonlyArray<{ id: string }> = [],
): QuickDepositState {
  const normalizedPreferredCurrencyId = preferredCurrencyId?.trim().toUpperCase();
  const preferredCurrency = normalizedPreferredCurrencyId
    ? currencies.find((currency) => currency.id.toUpperCase() === normalizedPreferredCurrencyId)?.id
    : undefined;

  return {
    amount: DEFAULT_AMOUNT,
    currency: preferredCurrency ?? currencies[0]?.id ?? DEFAULT_CURRENCY,
    note: DEFAULT_NOTE,
  };
}

export function normalizeQuickDepositState(state: QuickDepositState): QuickDepositState {
  return {
    amount: state.amount,
    currency: state.currency.trim().toUpperCase(),
    note: state.note.trim(),
  };
}

export function validateQuickDepositAmount(amount: number): string | null {
  return amount > 0 ? null : 'Введите сумму больше нуля';
}
