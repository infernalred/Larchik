import { OperationModel, OperationType } from './types';

const INSTRUMENT_OPERATION_TYPES = new Set<OperationType>([
  'Buy',
  'Sell',
  'Dividend',
  'BondPartialRedemption',
  'BondMaturity',
]);

const CASH_OPERATION_TYPES = new Set<OperationType>([
  'Deposit',
  'Withdraw',
  'Fee',
  'CashAdjustment',
]);

export function requiresOperationInstrument(type: OperationType): boolean {
  return INSTRUMENT_OPERATION_TYPES.has(type);
}

export function forbidsOperationInstrument(type: OperationType): boolean {
  return CASH_OPERATION_TYPES.has(type);
}

export function normalizeOperationFormModel(form: OperationModel): OperationModel {
  return {
    ...form,
    instrumentId: forbidsOperationInstrument(form.type) ? undefined : form.instrumentId || undefined,
    note: form.note || undefined,
  };
}

export function validateOperationFormModel(form: OperationModel): string | null {
  if (requiresOperationInstrument(form.type) && !form.instrumentId) {
    return 'Выберите инструмент для операции с бумагой.';
  }

  if (forbidsOperationInstrument(form.type) && form.instrumentId) {
    return 'Для денежной операции инструмент должен быть пустым.';
  }

  return null;
}
