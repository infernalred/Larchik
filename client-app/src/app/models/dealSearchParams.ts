export class DealSearchParams {
    ticker;
    type: number | undefined;

    constructor(ticker = "", type?: number | undefined) {
        this.ticker = ticker;
        this.type = type;
    }
}