import { useEffect, useState } from "react";
import { Route, Routes, useNavigate } from "react-router-dom";
import { ToastContainer } from "react-toastify";
import { Container } from "semantic-ui-react";
import AccountDetails from "../../features/accounts/AccountDetails";
import AccountList from "../../features/accounts/AccountList";
import DealForm from "../../features/deals/DealForm";
import DealList from "../../features/deals/DealList";
import NotFound from "../../features/errors/NotFound";
import ServerError from "../../features/errors/ServerError";
import HomePage from "../../features/home/HomePage";
import ReportForm from "../../features/reports/ReportForm";
import PortfolioList from "../../features/portfolio/PortfolioList";
import ModalContainer from "../common/modal/ModalContainer";
import { useStore } from "../store/store";
import LoadingComponent from "./LoadingComponent";
import Navbar from "./Navbar";
import AxiosInterceptorsSetup from "../api/AxiosInterceptorsSetup"
import { observer } from "mobx-react-lite";

function AxiosInterceptorNavigate() {
  const navigate = useNavigate();
  const [ran, setRan] = useState(false);

  if (!ran) {
      AxiosInterceptorsSetup(navigate);
      setRan(true);
  }
  
  return <></>;
}

function App() {
  const { commonStore, userStore } = useStore();

  useEffect(() => {
    if (commonStore.token) {
      userStore.getUser().finally(() => commonStore.setAppLoaded());
    } else {
      commonStore.setAppLoaded();
    }
  }, [commonStore, userStore])

  if (!commonStore.appLoaded) return <LoadingComponent content="Loading app..." />

  return (
    <>
      <ToastContainer position="bottom-right" hideProgressBar />
      <ModalContainer />
      <Routes>
        <Route index element={<HomePage />} />
        <Route path="/*" element={<LayoutsWithNavbar />}>
        </Route>
      </Routes>
    </>
  );

  function LayoutsWithNavbar() {
    return (
      <>
        <Navbar />
        <AxiosInterceptorNavigate/>
        <Container style={{ marginTop: "7em" }}>
          <Routes>
            <Route path="accounts" element={<AccountList />} />
            <Route path="accounts/:id/deals" element={<DealList />} />
            <Route path="accounts/:id" element={<AccountDetails />} />
            <Route path="deal" element={<DealForm />}>
              <Route path=":id" element={<DealForm />} />
            </Route>
            <Route path="reports" element={<ReportForm />} />
            <Route path="portfolio" element={<PortfolioList />}>
              <Route path=":id" element={<PortfolioList />} />
            </Route>
            <Route path="server-error" element={<ServerError />} />
            <Route path="*" element={<NotFound />} />
          </Routes>
        </Container>
      </>
    );
  }
}

export default observer(App);