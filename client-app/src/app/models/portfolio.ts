import { PortfolioAsset } from "./portfolioAsset";

export interface Portfolio {
    assets: PortfolioAsset[];
    totalBalance: number;
    profit: number;
}