import axios, { AxiosError, AxiosResponse } from "axios";
import { toast } from "react-toastify";
import { PaginatedResult } from "../models/pagination";
import { ServerError } from "../models/serverError";
import { store } from "../store/store";

const AxiosInterceptorsSetup = (navigate: any) => {
  axios.interceptors.response.use(
    async (response) => {
      const pagination = response.headers["pagination"];
      if (pagination) {
        response.data = new PaginatedResult(
          response.data,
          JSON.parse(pagination)
        );
        return response as AxiosResponse<PaginatedResult<any>>;
      }
      return response;
    },
    (error: AxiosError) => {
      // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
      const { data, status, headers } = error.response!;
      switch (status) {
        case 400:
          if ((data as any).errors) {
            const modalStateErrors = [] as string[];
            for (const key in (data as any).errors) {
              if ((data as any).errors[key]) {
                modalStateErrors.push((data as any).errors[key]);
              }
            }
            throw modalStateErrors.flat();
          } else {
            toast.error(data as string);
          }
          break;
        case 401:
          if (
            status === 401 &&
            headers["www-authenticate"]?.startsWith(
              'Bearer error="invalid_token"'
            )
          ) {
            store.userStore.logout();
            toast.error("Session expired - please login again");
          }
          break;
        case 404:
          navigate("/not-found");
          break;
        case 500:
          store.commonStore.setServerError(data as ServerError);
          navigate("/server-error");
          break;
      }
      return Promise.reject(error);
    }
  );
};

export default AxiosInterceptorsSetup;
