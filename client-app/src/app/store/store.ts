import { createContext, useContext } from "react";
import AccountStore from "./accountStore";
import CommonStore from "./commonStore";
import DealStore from "./dealStore";
import ModalStore from "./modalStore";
import UserStore from "./userStore";

interface Store {
    commonStore: CommonStore;
    userStore: UserStore;
    modalStore: ModalStore;
    accountStore: AccountStore;
    dealStore: DealStore;
}

export const store: Store = {
    commonStore: new CommonStore(),
    userStore: new UserStore(),
    modalStore: new ModalStore(),
    accountStore: new AccountStore(),
    dealStore: new DealStore()
}

export const StoreContext = createContext(store);

export function useStore() {
    return useContext(StoreContext);
}