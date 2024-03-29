import React from "react";
import ReactDOM from "react-dom/client";
import App from "./app/layout/App";
import reportWebVitals from "./reportWebVitals";
import "./app/layout/styles.css"
import { BrowserRouter } from "react-router-dom";
import { store, StoreContext } from "./app/store/store";
import "react-datepicker/dist/react-datepicker.css";
import "react-toastify/dist/ReactToastify.min.css";

const root = ReactDOM.createRoot(
  document.getElementById("root") as HTMLElement
);
root.render(
  <StoreContext.Provider value={store}>
      <BrowserRouter>
          <App />
      </BrowserRouter>
  </StoreContext.Provider>
);

// If you want to start measuring performance in your app, pass a function
// to log results (for example: reportWebVitals(console.log))
// or send to an analytics endpoint. Learn more: https://bit.ly/CRA-vitals
reportWebVitals();
