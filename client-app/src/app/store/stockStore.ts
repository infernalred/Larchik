import { makeAutoObservable, runInAction } from "mobx";
import agent from "../api/agent";
import { Stock } from "../models/stock";

export default class StockStore {
    stocks: Stock[] = [];
    loadingStocks = false;

    constructor() {
        makeAutoObservable(this)
    }

    setLoadingInitial = (state: boolean) => {
        this.loadingStocks = state;
    }

    loadStocks = async () => {
        this.setLoadingInitial(true);
        try {
            const stocks = (await agent.Stocks.list()).result;
            runInAction(() => {
                this.stocks = stocks;
            })
        } catch (error) {
            console.log(error);
        }
        finally {
            this.setLoadingInitial(false);
        }
    }

    get stocksSet() {
        return this.stocks.map(x => {
            return {key: x.ticker, value: x.ticker, text: x.ticker}
        })
    };
}