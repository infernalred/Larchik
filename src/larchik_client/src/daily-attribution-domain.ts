import { PositionDailyMove } from './types';

export const isDailyMoveDisplayable = (move: PositionDailyMove | undefined | null): boolean =>
  move?.dataQuality === 'complete';

export const getDailyMoveUnavailableReason = (dataQuality: string): string => {
  switch (dataQuality) {
    case 'missingPrice':
      return 'Нет цены хотя бы на одну дату отчёта';
    case 'missingFx':
      return 'Нет валютного курса хотя бы на одну дату отчёта';
    case 'stale':
      return 'Цена или курс старше даты отчёта';
    default:
      return 'Недостаточно рыночных данных для расчёта';
  }
};
