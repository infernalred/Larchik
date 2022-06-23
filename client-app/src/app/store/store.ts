import { createContext, useContext } from "react";
import AccountStore from "./accountStore";
import CommonStore from "./commonStore";
import CurrencyStore from "./currencyStore";
import DealStore from "./dealStore";
import ModalStore from "./modalStore";
import OperationStore from "./operationStore";
import PortfolioStore from "./portfolioStore";
import StockStore from "./stockStore";
import UserStore from "./userStore";

interface Store {
    commonStore: CommonStore;
    userStore: UserStore;
    modalStore: ModalStore;
    accountStore: AccountStore;
    dealStore: DealStore;
    operationStore: OperationStore;
    stockStore: StockStore;
    portfolioStore: PortfolioStore;
    currencyStore: CurrencyStore;
}

export const store: Store = {
    commonStore: new CommonStore(),
    userStore: new UserStore(),
    modalStore: new ModalStore(),
    accountStore: new AccountStore(),
    dealStore: new DealStore(),
    operationStore: new OperationStore(),
    stockStore: new StockStore(),
    portfolioStore: new PortfolioStore(),
    currencyStore: new CurrencyStore()
}

export const StoreContext = createContext(store);

export function useStore() {
    return useContext(StoreContext);
}