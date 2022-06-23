import { PortfolioAsset } from "./portfolioAsset";

export interface Portfolio {
    assets: PortfolioAsset[];
    totalBalance: number;
    totalBalanceAverage: number;
    profit: number;
}