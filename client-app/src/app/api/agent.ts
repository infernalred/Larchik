import axios, { AxiosError, AxiosResponse } from 'axios';
import { User, UserFormValues } from '../models/user';
import { store } from '../store/store';
import { history } from '../..';
import { toast } from 'react-toastify';
import { OperationResult } from '../models/operationResult';
import { Account } from '../models/account';

axios.defaults.baseURL = process.env.REACT_APP_API_URL!;

axios.interceptors.request.use(config => {
    const token = store.commonStore.token;
    if (token) config.headers!.Authorization = `Bearer ${token}`
    return config;
})

axios.interceptors.response.use(async response => {
    return response;
}, (error: AxiosError) => {
    const {data, status} = error.response!;
    switch (status) {
        case 400:
            if (data.errors) {
                const modalStateErrors = [];
                for (const key in data.error) {
                    if (data.error[key]) {
                        modalStateErrors.push(data.errors[key])
                    }
                }
                throw modalStateErrors.flat();
            } else {
                toast.error(data);
            }
            break;
        case 401:
            toast.error('Unauthorised');
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
    get: <T> (url: string) => axios.get<T>(url).then(responseBody),
    post: <T> (url: string, body: {}) => axios.post<T>(url, body).then(responseBody),
    put: <T> (url: string, body: {}) => axios.put<T>(url, body).then(responseBody),
    del: <T> (url: string) => axios.delete<T>(url).then(responseBody)
}

const Users = {
    login: (user: UserFormValues) => requests.post<User>('/user/login', user),
    current: () => requests.get<User>('/user'),
    register: (user: UserFormValues) => requests.post<User>('user/register', user)
}

const Accounts = {
    list: () => requests.get<OperationResult<Account[]>>('/accounts')

}

const agent = {
    Users,
    Accounts
}

export default agent;