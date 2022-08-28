export interface Stock {
    ticker: string;
    companyName: string;
    type: StockType;
    currency: string;
    sector: string;
    lastPrice: number
}

export enum StockType {
    Share = 1,
    Bond,
    Etf,
    Money
}