import { makeAutoObservable, runInAction } from "mobx";
import agent from "../api/agent";
import { Portfolio } from "../models/portfolio";

export default class PortfolioStore {
    portfolio: Portfolio | undefined = undefined;
    loadingPortfolio = false;

    constructor() {
        makeAutoObservable(this)
    }

    setLoadingInitial = (state: boolean) => {
        this.loadingPortfolio = state;
    }

    loadPortfolio = async () => {
        this.setLoadingInitial(true);
        try {
            const request = await agent.Portfolios.details();
            runInAction(() => {
                this.portfolio = request.result;
            })
        } catch (error) {
            console.log(error);
        }
        finally {
            this.setLoadingInitial(false);
        }
    }
}