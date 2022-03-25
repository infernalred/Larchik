import { makeAutoObservable, runInAction } from "mobx";
import agent from "../api/agent";
import { Account } from "../models/account";

export default class AccountStore {
    accountsRegistry = new Map<string, Account>();
    loadingInitial = false;
    selectedAccount: Account | undefined = undefined;

    constructor() {
        makeAutoObservable(this)
    }

    setLoadingInitial = (state: boolean) => {
        this.loadingInitial = state;
    }

    get accounts() {
        return Array.from(this.accountsRegistry.values())
    }

    loadAccounts = async () => {
        this.setLoadingInitial(true);
        try {
            const request = await agent.Accounts.list();
            runInAction(() => {
                request.result.forEach(account => {
                    this.accountsRegistry.set(account.id, account);
                })
                
            })
        } catch (error) {
            console.log(error)
        }
        finally {
            this.setLoadingInitial(false);
        }
    }

    loadAccount = async (id: string) => {
        let account = this.getAccount(id);
        if (account) {
            this.selectedAccount = account;
            return account;
        } else {
            this.setLoadingInitial(true);
            try {
                account = (await agent.Accounts.details(id)).result
                this.setAccount(account);
                runInAction(() => {
                    this.selectedAccount = account;
                })
                return account;
            } catch (error) {
                console.log(error);
            }
            finally {
                this.setLoadingInitial(false);
            }
        }
    }

    private setAccount = (account: Account) => {
        this.accountsRegistry.set(account.id, account);
    }

    private getAccount = (id: string) => {
        return this.accountsRegistry.get(id);
    }
}