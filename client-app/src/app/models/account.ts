import { Asset } from "./asset";
import { Broker } from "./broker";

export interface Account {
    id: string;
    broker: Broker;
    assets: Asset[]
}