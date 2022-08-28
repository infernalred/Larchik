import { makeAutoObservable, runInAction } from "mobx";
import agent from "../api/agent";
import { DealType } from "../models/dealType";

export default class DealTypeStore {
    dealTypesRegistry = new Map<string, DealType>();
    loadingDealTypes = false;

    constructor() {
        makeAutoObservable(this)
    }

    setLoadingInitial = (state: boolean) => {
        this.loadingDealTypes = state;
    }

    loadDealTypes = async () => {
        this.setLoadingInitial(true);
        try {
            const dealTypes = (await agent.DealTypes.list()).result;
            runInAction(() => {
                dealTypes.forEach(dealType => {
                    this.setDealType(dealType);
                })
            })
        } catch (error) {
            console.log(error);
        }
        finally {
            this.setLoadingInitial(false);
        }
    }

    get dealTypesSet() {
        return Array.from(this.dealTypesRegistry.values()).map(x => {
            return {key: x.id, value: x.id, text: x.code}
        })
    };

    private setDealType = (dealType: DealType) => {
        this.dealTypesRegistry.set(dealType.id.toString(), dealType);
    }
}