import { makeAutoObservable, runInAction } from "mobx";
import agent from "../api/agent";
import { Deal } from "../models/deal";

export default class DealStore {
    dealsRegistry = new Map<string, Deal>();
    loadingInitial = false;
    loading = false;
    selectedDeal: Deal | undefined = undefined;

    constructor() {
        makeAutoObservable(this)
    }

    setLoadingInitial = (state: boolean) => {
        this.loadingInitial = state;
    }

    get deals() {
        return Array.from(this.dealsRegistry.values());
    }

    loadDeals = async (id: string) => {
        this.setLoadingInitial(true);
        try {
            const request = await agent.Deals.list(id);
            runInAction(() => {
                request.result.forEach(deal => {
                    this.setDeal(id, deal);
                })
            })
        } catch (error) {
            console.log(error);
        }
        finally {
            this.setLoadingInitial(false);
        }
    }

    deleteDeal = async (id: string) => {
        this.loading = true;
        try {
            await agent.Deals.delete(id);
            runInAction(() => {
                this.dealsRegistry.delete(id);
            })
        } catch (error) {
            console.log(error);
        }
        finally {
            runInAction(() => {
                this.loading = false;
            })
        }
    }

    private setDeal = (id: string, deal: Deal) => {
        deal.createdAt = new Date(deal.createdAt);
        this.dealsRegistry.set(deal.id, deal);
    }

    private getDeal = (id: string) => {
        return this.dealsRegistry.get(id);
    }
}