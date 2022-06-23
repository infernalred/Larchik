import React, { useEffect, useState } from "react";
import { observer } from "mobx-react-lite";
import { useStore } from "../../app/store/store";
import { Link, useHistory, useParams } from "react-router-dom";
import { DealFormValues } from "../../app/models/deal";
import * as Yup from "yup";
import LoadingComponent from "../../app/layout/LoadingComponent";
import { Button, Header, Segment } from "semantic-ui-react";
import { Form, Formik } from "formik";
import MySelectOptions from "../../app/common/form/MySelectOptions";
import MyTextInput from "../../app/common/form/MyTextInput";
import MyDateInput from "../../app/common/form/MyDateInput";
import { v4 as uuid } from 'uuid';
import ru from 'date-fns/locale/ru'
import { registerLocale } from "react-datepicker";
registerLocale("ru", ru);


export default observer(function DealForm() {
    const history = useHistory();
    const { dealStore, operationStore, stockStore, accountStore, currencyStore } = useStore();
    const { createDeal, updateDeal, loadDeal, loadingDeals } = dealStore;
    const { loadOperations, loadingOperations, operationsSet } = operationStore;
    const { loadStocks, loadingStocks, stocksSet } = stockStore;
    const { loadAccounts, loadingInitial, accountSet } = accountStore;
    const { loadCurrencies, loadingCurrencies, currenciesSet } = currencyStore;
    const { id } = useParams<{ id: string }>();

    const [deal, setDeal] = useState<DealFormValues>(new DealFormValues());

    const validationSchema = Yup.object({
        isStock: Yup.boolean(),
        quantity: Yup.number().min(1, "Значение должно быть больше 0").required("Значение обязательно"),
        price: Yup.string().min(1, "Значение должно быть больше 0").required("Значение обязательно"),
        operation: Yup.string().required("Тип операции обязателен"),
        currency: Yup.string().required("Тип валюты обязателен"),
        stock: Yup.string().when("operation", {
            is: (value: string) => value === "Продажа" || value === "Покупка",
            then: Yup.string().required("Тикер обязателен"),
            otherwise: Yup.string().nullable()
        }),
        commission: Yup.string().required("Значение обязательно"),
        createdAt: Yup.string().required("Дата сделки обязательна").nullable(),
        accountId: Yup.string().required("Счет сделки обязателен")
    })

    useEffect(() => {
        loadStocks();
        loadOperations();
        loadAccounts();
        loadCurrencies();

        if (id) loadDeal(id).then(deal => setDeal(new DealFormValues(deal)))
    }, [id, loadDeal, loadStocks, loadOperations, loadAccounts, loadCurrencies]);



    function handleFormSubmit(deal: DealFormValues) {
        if (!deal.id) {
            let newDeal = {
                ...deal,
                id: uuid()
            };
            createDeal(newDeal).then(() => history.push(`/accounts/${newDeal.accountId}/deals`))
        } else {
            updateDeal(deal).then(() => history.push(`/accounts/${deal.accountId}/deals`))
        }
    }

    if (loadingDeals) return <LoadingComponent content="Loading deal..." />

    return (
        <Segment clearing>
            <Header content="Детали сделки" sub color="teal" />
            <Formik
                validationSchema={validationSchema}
                enableReinitialize
                initialValues={deal}
                validateOnMount={true}
                onSubmit={values => handleFormSubmit(values)}>
                {({ handleSubmit, isValid, isSubmitting, values }) => (
                    <Form className="ui form" onSubmit={handleSubmit} autoComplete="off">
                        <label>Тип</label>
                        <MySelectOptions placeholder="Тип" name="operation" options={operationsSet} loading={loadingOperations} />
                        <label>Валюта</label>
                        <MySelectOptions placeholder="Валюта" name="currency" options={currenciesSet} loading={loadingCurrencies} />
                        <label>Цена</label>
                        <MyTextInput placeholder="Цена" name="price" type="number" />
                        <label>Комиссия</label>
                        <MyTextInput placeholder="Комиссия" name="commission" type="number" />
                        {(values.operation === "Покупка" || values.operation === "Продажа") &&
                            <>
                                <label>Кол-во</label>
                                <MyTextInput placeholder="Кол-во" name="quantity" type="number" />
                                <label>Тикер</label>
                                <MySelectOptions placeholder="Тикер" name="stock" options={stocksSet} loading={loadingStocks} />
                            </>}
                        <label>Дата сделки</label>
                        <MyDateInput
                            placeholderText="Дата"
                            name="createdAt"
                            showTimeSelect
                            timeCaption="Время"
                            closeOnScroll={true}
                            locale="ru"
                            timeFormat="p"
                            dateFormat="d.MM.yyyy HH:mm:ss"
                        />
                        <label>Счет сделки</label>
                        <MySelectOptions placeholder="Счет" name="accountId" options={accountSet} loading={loadingInitial} />
                        <Button
                            disabled={isSubmitting || !isValid}
                            loading={isSubmitting} floated='right'
                            positive type='submit' content='Submit' />
                        <Button as={Link} to={deal.id ? `/accounts/${deal.accountId}/deals` : '/accounts/'} floated='right' type='button' content='Cancel' />
                    </Form>
                )}
            </Formik>
        </Segment>
    )
})