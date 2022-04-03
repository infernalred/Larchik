import { makeAutoObservable, runInAction } from "mobx";
import agent from "../api/agent";
import { Operation } from "../models/operation";

export default class OperationStore {
    operations: Operation[] = [];
    loadingOperations = false;

    constructor() {
        makeAutoObservable(this)
    }

    setLoadingInitial = (state: boolean) => {
        this.loadingOperations = state;
    }

    loadOperations = async () => {
        this.setLoadingInitial(true);
        try {
            const operations = (await agent.Operations.list()).result;
            runInAction(() => {
                this.operations = operations;
            })
        } catch (error) {
            console.log(error);
        }
        finally {
            this.setLoadingInitial(false);
        }
    }

    get operationsSet() {
        return this.operations.map(x => {
            return {key: x.code, value: x.code, text: x.code}
        })
    };
}