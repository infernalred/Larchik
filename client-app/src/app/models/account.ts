import { Asset } from "./asset";

export interface Account {
    id: string;
    name: string;
    assets: Asset[];
}

export class Account implements Account {
    constructor(init?: AccountFormValues) {
      Object.assign(this, init);
    }
  }

export class AccountFormValues {
    id?: string = undefined;
    name: string = '';

    constructor(account?: AccountFormValues) {
        if (account) {
            this.id = account.id;
            this.name = account.name;
        }
    }
}