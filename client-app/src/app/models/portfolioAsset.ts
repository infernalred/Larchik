export interface PortfolioAsset {
    ticker: string;
    companyName: string;
    sector: string;
    type: string;
    quantity: number;
    price: number;
    amountMarket: number;
    amountMarketCurrency: number;
    averagePrice: number;
    amountAverage: number;
    amountAverageCurrency: number;
    profitCurrency: number;
}