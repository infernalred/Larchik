import { Stock } from "./stock";

export interface PortfolioAsset {
    stock: Stock;
    quantity: number;
    amountMarket: number;
    amountMarketCurrency: number;
    averagePrice: number;
    amountAverage: number;
    profit: number;
}