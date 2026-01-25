export type InstrumentType = 'Equity' | 'Bond' | 'Etf' | 'Currency' | 'Commodity' | 'Crypto';

export type OperationType =
  | 'Buy'
  | 'Sell'
  | 'Dividend'
  | 'Fee'
  | 'Deposit'
  | 'Withdraw'
  | 'TransferIn'
  | 'TransferOut';

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

export interface PortfolioSummary {
  id: string;
  name: string;
  reportingCurrencyId: string;
  netInflowBase: number;
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

export interface CashBalance {
  currencyId: string;
  amount: number;
  amountInBase: number;
}

export interface PositionHolding {
  instrumentId: string;
  instrumentName: string;
  currencyId: string;
  quantity: number;
  lastPrice?: number;
  marketValueBase: number;
  averageCost: number;
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
  returnPct: number;
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
  createdAt: string;
  updatedAt: string;
}
