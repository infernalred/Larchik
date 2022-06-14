import { makeAutoObservable, runInAction } from "mobx";
import agent from "../api/agent";
import { Currency } from "../models/currency";

export default class CurrencyStore {
    currencies: Currency[] = [];
    loadingCurrencies = false;

    constructor() {
        makeAutoObservable(this)
    }

    setLoadingInitial = (state: boolean) => {
        this.loadingCurrencies = state;
    }

    loadCurrencies = async () => {
        this.setLoadingInitial(true);
        try {
            const currencies = (await agent.Currencies.list()).result;
            runInAction(() => {
                this.currencies = currencies;
            })
        } catch (error) {
            console.log(error);
        }
        finally {
            this.setLoadingInitial(false);
        }
    }

    get currenciesSet() {
        return this.currencies.map(x => {
            return {key: x.code, value: x.code, text: x.code}
        })
    };
}