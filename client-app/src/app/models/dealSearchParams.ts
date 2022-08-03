export class DealSearchParams {
    ticker;
    operation;

    constructor(ticker = "", operation = "") {
        this.ticker = ticker;
        this.operation = operation;
    }
}