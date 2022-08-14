import { makeAutoObservable, runInAction } from "mobx";
import agent from "../api/agent";
import { Deal, DealFormValues } from "../models/deal";
import { DealSearchParams } from "../models/dealSearchParams";
import { Pagination, PagingParams} from "../models/pagination";

export default class DealStore {
    dealsRegistry = new Map<string, Deal>();
    loadingDeals = false;
    loading = false;
    selectedDeal: Deal | undefined = undefined;
    pagination: Pagination | null = null;
    pagingParams = new PagingParams();
    dealSearchParams = new DealSearchParams();

    constructor() {
        makeAutoObservable(this)
    }

    setPagingParams = (pagingParams: PagingParams) => {
        this.pagingParams = pagingParams;
    }

    setDealSearchParams = (dealSearchParams: DealSearchParams) => {
        this.dealSearchParams = dealSearchParams;
    }

    get axiosParams() {
        const params = new URLSearchParams();
        params.append("pageNumber", this.pagingParams.pageNumber.toString());
        params.append("pageSize", this.pagingParams.pageSize.toString());

        if (this.dealSearchParams.ticker) {
            params.append("ticker", this.dealSearchParams.ticker);
        }

        if (this.dealSearchParams.operation) {
            params.append("operation", this.dealSearchParams.operation);
        }
        return params;
    }

    setLoadingInitial = (state: boolean) => {
        this.loadingDeals = state;
    }

    get deals() {
        return Array.from(this.dealsRegistry.values()).sort((a, b) => 
        b.createdAt.getTime() - a.createdAt.getTime());
    }

    loadDeals = async (id: string) => {
        this.dealsRegistry.clear();
        this.setLoadingInitial(true);
        try {
            const request = await agent.Deals.list(id, this.axiosParams);
            runInAction(() => {
                request.data.result.forEach(deal => {
                    this.setDeal(deal);
                })
            })
            this.setPagination(request.pagination)
        } catch (error) {
            console.log(error);
        }
        finally {
            this.setLoadingInitial(false);
        }
    }

    setPagination = (pagination: Pagination) => {
        this.pagination = pagination;
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