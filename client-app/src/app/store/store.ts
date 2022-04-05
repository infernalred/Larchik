import { createContext, useContext } from "react";
import AccountStore from "./accountStore";
import CommonStore from "./commonStore";
import DealStore from "./dealStore";
import ModalStore from "./modalStore";
import OperationStore from "./operationStore";
import ReportStore from "./reportStore";
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
    reportStore: ReportStore;
}

export const store: Store = {
    commonStore: new CommonStore(),
    userStore: new UserStore(),
    modalStore: new ModalStore(),
    accountStore: new AccountStore(),
    dealStore: new DealStore(),
    operationStore: new OperationStore(),
    stockStore: new StockStore(),
    reportStore: new ReportStore()
}

export const StoreContext = createContext(store);

export function useStore() {
    return useContext(StoreContext);
}