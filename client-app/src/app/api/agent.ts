import axios, { AxiosResponse } from 'axios';
import { User, UserFormValues } from '../models/user';
import { store } from '../store/store';
import { OperationResult } from '../models/operationResult';
import { Account, AccountFormValues } from '../models/account';
import { Deal, DealFormValues } from '../models/deal';
import { DealType } from '../models/dealType';
import { Stock } from '../models/stock';
import { Portfolio } from '../models/portfolio';
import { Currency } from '../models/currency';
import { PaginatedResult } from '../models/pagination';

axios.defaults.baseURL = process.env.REACT_APP_API_URL!;

axios.interceptors.request.use(config => {
    const token = store.commonStore.token;
    if (token) config.headers!.Authorization = `Bearer ${token}`
    return config;
})

const responseBody = <T>(response: AxiosResponse<T>) => response.data;

const requests = {
    get: <T>(url: string) => axios.get<T>(url).then(responseBody),
    post: <T>(url: string, body: {}) => axios.post<T>(url, body).then(responseBody),
    put: <T>(url: string, body: {}) => axios.put<T>(url, body).then(responseBody),
    del: <T>(url: string) => axios.delete<T>(url).then(responseBody)
}

const Users = {
    login: (user: UserFormValues) => requests.post<User>('/user/login', user),
    current: () => requests.get<User>('/user'),
    register: (user: UserFormValues) => requests.post<User>('user/register', user)
}

const Accounts = {
    list: () => requests.get<OperationResult<Account[]>>('/accounts'),
    details: (id: string) => requests.get<OperationResult<Account>>(`/accounts/${id}`),
    create: (account: AccountFormValues) => requests.post<void>('/accounts', account),
    update: (account: AccountFormValues) => requests.put<void>(`/accounts/${account.id}`, account)
}

const Deals = {
    list: (id: string, params: URLSearchParams) => axios.get<PaginatedResult<OperationResult<Deal[]>>>(`/deals/accounts/${id}`, {params}).then(responseBody),
    delete: (id: string) => requests.del<void>(`/deals/${id}`),
    create: (deal: DealFormValues) => requests.post<void>('/deals', deal),
    update: (deal: DealFormValues) => requests.put<void>(`/deals/${deal.id}`, deal),
    details: (id: string) => requests.get<OperationResult<Deal>>(`/deals/${id}`)
}

const DealTypes = {
    list: () => requests.get<OperationResult<DealType[]>>('/dealTypes')
}

const Stocks = {
    list: () => requests.get<OperationResult<Stock[]>>('/stocks')
}

const Portfolios = {
    details: () => requests.get<OperationResult<Portfolio>>('/portfolio'),
    accountDetails: (id: string) => requests.get<OperationResult<Portfolio>>(`/portfolio/${id}`)
}

const Currencies = {
    list: () => requests.get<OperationResult<Currency[]>>('/currency')
}

const agent = {
    Users,
    Accounts,
    Deals,
    DealTypes,
    Stocks,
    Portfolios,
    Currencies
}

export default agent;