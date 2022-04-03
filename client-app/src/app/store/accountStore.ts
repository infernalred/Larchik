import { makeAutoObservable, runInAction } from "mobx";
import agent from "../api/agent";
import { Account, AccountFormValues } from "../models/account";
import { store } from "./store";

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

    createAccount = async (account: AccountFormValues) => {
        try {
            await agent.Accounts.create(account);
            const newAccount = new Account(account);
            newAccount.assets = [];
            this.setAccount(newAccount);
            store.modalStore.closeModal();
        } catch (error) {
            throw (error);
        }
    }

    updateAccount = async (account: AccountFormValues) => {
        console.log('обновляем')
        console.log(account);
        try {
            await agent.Accounts.update(account);
            runInAction(() => {
                if (account.id) {
                    let updateAccount = {...this.getAccount(account.id), ...account}
                    this.accountsRegistry.set(account.id, updateAccount as Account)
                    store.modalStore.closeModal();
                }
            })
        } catch (error) {
            throw (error);
        }
    }

    get accountSet() {
        return this.accounts.map(x => {
            return {key: x.id, value: x.id, text: x.name}
        })
    };

    private setAccount = (account: Account) => {
        this.accountsRegistry.set(account.id, account);
    }

    private getAccount = (id: string) => {
        return this.accountsRegistry.get(id);
    }
}