import { makeAutoObservable, runInAction } from "mobx";
import agent from "../api/agent";
import { Account } from "../models/account";
import { store } from "./store";

export default class AccountStore {
    accounts: Account[] = [];
    loadingAccounts = false;

    constructor() {
        makeAutoObservable(this)
    }

    setLoadingInitial = (state: boolean) => {
        this.loadingAccounts = state;
    }

    loadAccounts = async () => {
        this.setLoadingInitial(true);
        try {
            const result = await agent.Accounts.list();
            runInAction(() => {
                this.accounts = result.result;
                console.log(this.accounts)
            })
        } catch (error) {
            console.log(error)
        }
        finally {
            this.setLoadingInitial(false);
        }
    }
}