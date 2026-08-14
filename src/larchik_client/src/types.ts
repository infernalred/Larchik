export type InstrumentType = 'Equity' | 'Bond' | 'Etf' | 'Currency' | 'Commodity' | 'Crypto';
export type PriceSource = 'MOEX' | 'TBANK';
export type CorporateActionType = 'Split' | 'ReverseSplit';

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
  | 'CashAdjustment';

export interface User {
  id: string;
  email: string;
  username: string;
  emailConfirmed: boolean;
  roles: string[];
  isAdmin: boolean;
}

export interface Category {
  id: number;
  name: string;
}

export interface Currency {
  id: string;
  name: string;
}

export interface CurrencyModel {
  id: string;
  name: string;
}

export interface UpdateCurrencyModel {
  name: string;
}

export interface ReferenceItem {
  id: string;
  name: string;
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
  isin?: string | null;
  figi?: string;
  currencyId: string;
}

export interface InstrumentModel {
  name: string;
  ticker: string;
  isin?: string | null;
  figi?: string;
  type: InstrumentType;
  currencyId: string;
  categoryId: number;
  exchange?: string;
  country?: string;
  isTrading: boolean;
  priceSource?: PriceSource | null;
}

export interface Instrument extends InstrumentModel {
  id: string;
  isTrading: boolean;
}

export interface InstrumentCorporateActionModel {
  type: CorporateActionType;
  factor: number;
  effectiveDate: string;
  note: string;
}

export interface InstrumentCorporateAction extends InstrumentCorporateActionModel {
  id: string;
  instrumentId: string;
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
  pnlBase: number;
  annualizedReturnPct?: number | null;
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
  dailyMove?: PositionDailyMove;
}

export interface PositionDailyMove {
  pnlBase: number;
  returnPct: number | null;
  priceEffectBase: number;
  fxEffectBase: number;
  crossEffectBase: number;
  tradingEffectBase: number;
  incomeEffectBase: number;
  feeEffectBase: number;
  otherEffectBase: number;
  dataQuality: string;
}

export interface PositionDailyPnlAttribution extends PositionDailyMove {
  instrumentId: string;
  instrumentName: string;
  instrumentType?: InstrumentType;
  categoryName?: string;
  currencyId: string;
  startQuantity: number;
  endQuantity: number;
  startPrice: number | null;
  endPrice: number | null;
  startPriceDate: string | null;
  endPriceDate: string | null;
  startFxRate: number | null;
  endFxRate: number | null;
  startFxRateDate: string | null;
  endFxRateDate: string | null;
  startMarketValueBase: number;
  endMarketValueBase: number;
  priceReturnPct: number | null;
  fxReturnPct: number | null;
  totalMarketReturnPct: number | null;
  warnings: string[];
}

export interface CashDailyPnlAttribution {
  currencyId: string;
  startAmount: number;
  endAmount: number;
  startFxRate: number | null;
  endFxRate: number | null;
  fxEffectBase: number;
  dataQuality: string;
}

export interface DailyPnlAttribution {
  portfolioId: string | null;
  name: string;
  reportingCurrencyId: string;
  comparisonDate: string;
  valuationDate: string;
  startNavBase: number;
  endNavBase: number;
  externalFlowBase: number;
  pnlBase: number;
  returnPct: number | null;
  priceEffectBase: number;
  securityFxEffectBase: number;
  crossEffectBase: number;
  tradingEffectBase: number;
  cashFxEffectBase: number;
  fxEffectBase: number;
  incomeEffectBase: number;
  feeEffectBase: number;
  otherEffectBase: number;
  reconciliationResidualBase: number;
  isComplete: boolean;
  warnings: string[];
  positions: PositionDailyPnlAttribution[];
  cash: CashDailyPnlAttribution[];
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
  warnings: string[];
}

export interface ClearPortfolioDataResult {
  deletedOperations: number;
  deletedPositionSnapshots: number;
  deletedPortfolioSnapshots: number;
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
