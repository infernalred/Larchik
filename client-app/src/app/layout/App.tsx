import { observer } from 'mobx-react-lite';
import React, { useEffect } from 'react';
import { Route, Switch, useLocation } from 'react-router-dom';
import { ToastContainer } from 'react-toastify';
import { Container } from 'semantic-ui-react';
import AccountDetails from '../../features/accounts/AccountDetails';
import AccountList from '../../features/accounts/AccountList';
import DealForm from '../../features/deals/DealForm';
import DealList from '../../features/deals/DealList';
import NotFound from '../../features/errors/NotFound';
import ServerError from '../../features/errors/ServerError';
import HomePage from '../../features/home/HomePage';
import ReportForm from '../../features/reports/ReportForm';
import PortfolioList from '../../features/portfolio/PortfolioList';
import ModalContainer from '../common/modal/ModalContainer';
import { useStore } from '../store/store';
import LoadingComponent from './LoadingComponent';
import Navbar from './Navbar';

function App() {
  const location = useLocation();
  const { commonStore, userStore } = useStore();

  useEffect(() => {
    if (commonStore.token) {
      userStore.getUser().finally(() => commonStore.setAppLoaded());
    } else {
      commonStore.setAppLoaded();
    }
  }, [commonStore, userStore])

  if (!commonStore.appLoaded) return <LoadingComponent content='Loading app...' />

  return (
    <>
      <ToastContainer position='bottom-right' hideProgressBar />
      <ModalContainer />
      <Route exact path='/' component={HomePage} />
      <Route
        path={'/(.+)'}
        render={() => (
          <>
            <Navbar />
            <Container style={{ marginTop: '7em' }}>
              <Switch>
                <Route exact path='/accounts' component={AccountList} />
                <Route path='/accounts/:id/deals' component={DealList} />
                <Route path='/accounts/:id' component={AccountDetails} />
                <Route key={location.key} path={['/deal/:id', '/deal']} component={DealForm}/>
                <Route path='/reports' component={ReportForm} />
                <Route path='/portfolio' component={PortfolioList} />
                <Route path='/server-error' component={ServerError} />
                <Route component={NotFound} />
              </Switch>
            </Container>
          </>
        )}
      />
    </>
  );
}

export default observer(App);
