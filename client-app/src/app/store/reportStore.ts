import { makeAutoObservable } from "mobx";
import { CurrencyOperationsReport } from "../models/reports/currencyOperationsReport";

export default class ReportStore {
    currencyReport: CurrencyOperationsReport| undefined = undefined;
    initialLoading = false;

    constructor() {
        makeAutoObservable(this)
    }

    setLoadingInitial = (state: boolean) => {
        this.initialLoading = state;
    }
}