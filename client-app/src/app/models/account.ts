import { Asset } from "./asset";

export interface Account {
    id: string;
    name: string;
    assets: Asset[]
}