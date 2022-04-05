import axios, { AxiosError, AxiosResponse } from 'axios';
import { User, UserFormValues } from '../models/user';
import { store } from '../store/store';
import { history } from '../..';
import { toast } from 'react-toastify';
import { OperationResult } from '../models/operationResult';
import { Account, AccountFormValues } from '../models/account';
import { Deal, DealFormValues } from '../models/deal';
import { Operation } from '../models/operation';
import { Stock } from '../models/stock';
import { CurrencyOperationsReport } from '../models/reports/currencyOperationsReport';

axios.defaults.baseURL = process.env.REACT_APP_API_URL!;

axios.interceptors.request.use(config => {
    const token = store.commonStore.token;
    if (token) config.headers!.Authorization = `Bearer ${token}`
    return config;
})

axios.interceptors.response.use(async response => {
    return response;
}, (error: AxiosError) => {
    const { data, status, config, headers } = error.response!;
    switch (status) {
        case 400:
            if (data.errors) {
                const modalStateErrors = [];
                for (const key in data.errors) {
                    if (data.errors[key]) {
                        modalStateErrors.push(data.errors[key])
                    }
                }
                throw modalStateErrors.flat();
            } else {
                toast.error(data);
            }
            break;
        case 401:
            if (status === 401 && headers['www-authenticate']?.startsWith('Bearer error="invalid_token"')) {
                store.userStore.logout();
                toast.error('Session expired - please login again');
            }
            break;
        case 404:
            history.push('/not-found');
            break;
        case 500:
            store.commonStore.setServerError(data);
            history.push('/server-error')
            break;
    }
    return Promise.reject(error);
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
    list: (id: string) => requests.get<OperationResult<Deal[]>>(`/deals/accounts/${id}`),
    delete: (id: string) => requests.del<void>(`/deals/${id}`),
    create: (deal: DealFormValues) => requests.post<void>('/deals', deal),
    update: (deal: DealFormValues) => requests.put<void>(`/deals/${deal.id}`, deal),
    details: (id: string) => requests.get<OperationResult<Deal>>(`/deals/${id}`)
}

const Operations = {
    list: () => requests.get<OperationResult<Operation[]>>('/operations')
}

const Stocks = {
    list: () => requests.get<OperationResult<Stock[]>>('/stocks')
}

const Reports = {
    currency: (id: string, params: ReportParams) => axios.get<OperationResult<CurrencyOperationsReport>>(`/reports/${id}`, { params })
        .then(responseBody)
}

const agent = {
    Users,
    Accounts,
    Deals,
    Operations,
    Stocks
}

export default agent;