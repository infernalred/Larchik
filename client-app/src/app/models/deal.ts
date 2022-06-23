export interface Deal {
    id: string;
    quantity: number;
    price: number;
    operation: string;
    currency: string;
    stock: string | undefined;
    commission: number;
    createdAt: Date;
    accountId: string;
}

export class Deal implements Deal {
    constructor(init?: DealFormValues) {
      Object.assign(this, init);
    }
  }

export class DealFormValues {
    id: string = '';
    quantity: number = 1;
    price: number = 0;
    operation: string = '';
    currency: string = '';
    stock: string | undefined;
    commission: number = 0;
    createdAt: Date = new Date();
    accountId: string = '';

    constructor(deal?: DealFormValues) {
        if (deal) {
            this.id = deal.id;
            this.quantity = deal.quantity;
            this.price = deal.price;
            this.operation = deal.operation;
            this.currency = deal.currency;
            this.stock = deal.stock;
            this.commission = deal.commission;
            this.createdAt = deal.createdAt;
            this.accountId = deal.accountId;
        }
    }
}