import { makeAutoObservable, runInAction } from "mobx";
import agent from "../api/agent";
import { User, UserFormValues } from "../models/user";
import { store } from "./store";

export default class UserStore {
    user: User | null = null;

    constructor() {
        makeAutoObservable(this)
    }

    get isLoggedIn() {
        return !!this.user;
    }

    login = async (creds: UserFormValues) => {
        try {
            const user = await agent.Users.login(creds);
            store.commonStore.setToken(user.token);
            runInAction(() => 
                this.user = user
            );
            store.modalStore.closeModal();
        } catch (error) {
            throw error;
        }
    }

    getUser = async () => {
        try {
            const user = await agent.Users.current();
            runInAction(() => this.user = user);
        } catch (error) {
            console.error();
        }
    }

    logout = () => {
        store.commonStore.setToken(null);
        window.localStorage.removeItem('jwt');
        this.user = null;
    }

    register = async (creds: UserFormValues) => {
        try {
            const user = await agent.Users.register(creds);
            store.commonStore.setToken(user.token);
            runInAction(() => 
                this.user = user
            );
            store.modalStore.closeModal();
        } catch (error) {
            throw error;
        }
    }
}