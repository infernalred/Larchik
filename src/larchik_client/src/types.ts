export type InstrumentType = 'Equity' | 'Bond' | 'Etf' | 'Currency' | 'Commodity' | 'Crypto';

export type OperationType =
  | 'Buy'
  | 'Sell'
  | 'Dividend'
  | 'Fee'
  | 'Deposit'
  | 'Withdraw'
  | 'TransferIn'
  | 'TransferOut'
  | 'BondPartialRedemption'
  | 'BondMaturity'
  | 'Split'
  | 'ReverseSplit'
  | 'CashAdjustment';

export interface User {
  id: string;
  email: string;
  username: string;
  emailConfirmed: boolean;
  roles: string[];
  isAdmin: boolean;
}

export interface Portfolio {
  id: string;
  name: string;
  reportingCurrencyId: string;
  brokerId?: string;
}

export interface Broker {
  id: string;
  code?: string;
  name: string;
  country?: string;
  supportsImport: boolean;
}

export interface InstrumentLookup {
  id: string;
  name: string;
  ticker: string;
  isin: string;
  figi?: string;
  currencyId: string;
}

export interface PortfolioSummary {
  id: string;
  name: string;
  reportingCurrencyId: string;
  netInflowBase: number;
  grossDepositsBase: number;
  grossWithdrawalsBase: number;
  cashBase: number;
  positionsValueBase: number;
  realizedBase: number;
  unrealizedBase: number;
  navBase: number;
  valuationMethod: string;
  cash: CashBalance[];
  positions: PositionHolding[];
  realizedByInstrument: RealizedPnl[];
}

export interface PortfoliosSummary {
  reportingCurrencyId: string;
  portfolioCount: number;
  netInflowBase: number;
  grossDepositsBase: number;
  grossWithdrawalsBase: number;
  cashBase: number;
  positionsValueBase: number;
  realizedBase: number;
  unrealizedBase: number;
  pnlBase: number;
  valuationMethod: string;
  navBase: number;
}

export interface CashBalance {
  currencyId: string;
  amount: number;
  amountInBase: number;
}

export interface PositionHolding {
  instrumentId: string;
  instrumentName: string;
  instrumentType?: InstrumentType;
  categoryName?: string;
  currencyId: string;
  priceCurrencyId?: string;
  averageCostCurrencyId?: string;
  quantity: number;
  lastPrice?: number;
  marketValueBase: number;
  averageCost: number;
  isCash?: boolean;
  localAmount?: number;
}

export interface RealizedPnl {
  instrumentId: string;
  instrumentName: string;
  currencyId: string;
  realized: number;
  realizedBase: number;
}

export interface PortfolioPerformance {
  period: string; // yyyy-MM
  startDate: string;
  endDate: string;
  reportingCurrencyId: string;
  valuationMethod: string;
  startNavBase: number;
  endNavBase: number;
  netInflowBase: number;
  pnlBase: number;
  returnPct: number | null;
  realizedBase: number;
  unrealizedBase: number;
}

export interface OperationModel {
  instrumentId?: string;
  type: OperationType;
  quantity: number;
  price: number;
  fee: number;
  currencyId: string;
  tradeDate: string;
  settlementDate?: string;
  note?: string;
}

export interface Operation extends OperationModel {
  id: string;
  portfolioId: string;
  instrumentTicker?: string;
  createdAt: string;
  updatedAt: string;
}

export interface ImportResult {
  importedOperations: number;
  skippedOperations: number;
  errors: string[];
}

export interface ClearPortfolioDataResult {
  deletedOperations: number;
  deletedPositionSnapshots: number;
  deletedPortfolioSnapshots: number;
  deletedLots: number;
  deletedCashBalances: number;
}

export interface RecalculatePortfolioResult {
  recalculatedFromDate: string;
  operationCount: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
