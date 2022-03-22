import { Stock } from "./stock";

export interface Asset {
    id: string;
    stock: Stock;
    quantity: number;
}