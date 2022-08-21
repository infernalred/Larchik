import { Stock } from "./stock";

export interface PortfolioAsset {
    stock: Stock;
    // ticker: string;
    // companyName: string;
    // sector: string;
    // type: string;
    rate: number;
    quantity: number;
    //price: number;
    amountMarket: number;
    amountMarketCurrency: number;
    averagePrice: number;
    amountAverage: number;
    profit: number;
}