import { Form, Formik } from 'formik'
import { observer } from 'mobx-react-lite';
import React, { useEffect, useState } from 'react'
import { Header, Button } from 'semantic-ui-react';
import MyTextInput from '../../app/common/form/MyTextInput';
import { useStore } from '../../app/store/store';
import * as Yup from 'yup';
import { v4 as uuid } from 'uuid';
import { AccountFormValues } from '../../app/models/account';


interface Props {
    id: string;
}

export default observer(function AccountForm({ id }: Props) {
    const { accountStore, modalStore } = useStore();
    const { createAccount, updateAccount, loadAccount } = accountStore;

    const [account, setAccount] = useState<AccountFormValues>(new AccountFormValues());

    const validationSchema = Yup.object({
        name: Yup.string().trim().min(1).max(15, 'Максимум 15 символов').required('Название обязательно')
    })

    useEffect(() => {
        if (id) loadAccount(id).then(account => setAccount(new AccountFormValues(account)))
    }, [id, loadAccount])

    function handleFormSubmit(account: AccountFormValues) {
        if (!account.id) {
            let newAccount = {
                ...account,
                id: uuid()
            };
            createAccount(newAccount)
        } else {
            updateAccount(account)
        }
    }

    return (
        <Formik
            validationSchema={validationSchema}
            enableReinitialize
            initialValues={account}
            onSubmit={values => handleFormSubmit(values)}>
            {({ handleSubmit, isValid, isSubmitting, dirty }) => (
                <Form className='ui form' onSubmit={handleSubmit} autoComplete='off'>
                    <Header as='h2' content='Создать счет' color='teal' textAlign='center' />
                    <MyTextInput name='name' placeholder='Название' />
                    <Button type="button" onClick={() => modalStore.closeModal()} floated='right' content='Отмена' />
                    <Button
                        style={{ marginBottom: 10 }}
                        disabled={isSubmitting || !dirty || !isValid}
                        loading={isSubmitting}
                        floated='right'
                        positive type='submit' content='Создать'></Button>
                </Form>
            )}
        </Formik>
    )
})