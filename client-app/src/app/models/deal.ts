export interface Deal {
    id: string;
    quantity: number;
    price: number;
    operation: string;
    stock: string;
    commission: number;
    createdAt: Date;
}