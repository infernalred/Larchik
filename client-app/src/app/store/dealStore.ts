import { makeAutoObservable, runInAction } from "mobx";
import agent from "../api/agent";
import { Deal, DealFormValues } from "../models/deal";

export default class DealStore {
    dealsRegistry = new Map<string, Deal>();
    loadingDeals = false;
    loading = false;
    selectedDeal: Deal | undefined = undefined;

    constructor() {
        makeAutoObservable(this)
    }

    setLoadingInitial = (state: boolean) => {
        this.loadingDeals = state;
    }

    get deals() {
        return Array.from(this.dealsRegistry.values()).sort((a, b) => 
        b.createdAt.getTime() - a.createdAt.getTime());
    }

    loadDeals = async (id: string) => {
        this.setLoadingInitial(true);
        try {
            const request = await agent.Deals.list(id);
            runInAction(() => {
                request.result.forEach(deal => {
                    this.setDeal(deal);
                })
            })
        } catch (error) {
            console.log(error);
        }
        finally {
            this.setLoadingInitial(false);
        }
    }

    loadDeal = async (id: string) => {
        let deal = this.getDeal(id);
        if (deal) {
            this.selectedDeal = deal;
            return deal;
        } else {
            this.loadingDeals = true;
            try {
                deal = (await agent.Deals.details(id)).result;
                this.setDeal(deal);
                runInAction(() => {
                    this.selectedDeal = deal;
                })
                return deal;
            } catch (error) {
                console.log(error);
            }
            finally {
                this.setLoadingInitial(false);
            }
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

    createDeal = async (deal: DealFormValues) => {
        try {
            await agent.Deals.create(deal);
            const newDeal = new Deal(deal);
            this.setDeal(newDeal);
            runInAction(() => {
                this.selectedDeal = newDeal;
            })
        } catch (error) {
            console.log(error);
        }
    }

    updateDeal = async (deal: DealFormValues) => {
        try {
            await agent.Deals.update(deal);
            runInAction(() => {
                if (deal.id) {
                    let updateDeal = {...this.getDeal(deal.id), ...deal}
                    this.dealsRegistry.set(deal.id, updateDeal as Deal);
                    this.selectedDeal = updateDeal as Deal;
                }
            })
        } catch (error) {
            console.log(error);
        }
    }

    private setDeal = (deal: Deal) => {
        deal.createdAt = new Date(deal.createdAt);
        this.dealsRegistry.set(deal.id, deal);
    }

    private getDeal = (id: string) => {
        return this.dealsRegistry.get(id);
    }
}