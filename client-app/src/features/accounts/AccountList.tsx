import { observer } from "mobx-react-lite"
import React, { useEffect } from "react"
import { NavLink } from "react-router-dom";
import { Button, Icon, Table } from "semantic-ui-react"
import LoadingComponent from "../../app/layout/LoadingComponent";
import { useStore } from "../../app/store/store";
import AccountForm from "./AccountForm";

export default observer(function AccountList() {
    const { accountStore, modalStore } = useStore();
    const { loadAccounts, accounts, accountsRegistry } = accountStore;

    useEffect(() => {
        if (accountsRegistry.size <= 1) loadAccounts();
    }, [accountsRegistry.size, loadAccounts])

    if (accountStore.loadingInitial) return <LoadingComponent content='Loading accounts...' />

    return (
        <>
            <Table celled inverted>
                <Table.Header>
                    <Table.Row>
                        <Table.HeaderCell>Название</Table.HeaderCell>
                        <Table.HeaderCell>Активов</Table.HeaderCell>
                        <Table.HeaderCell>Действия</Table.HeaderCell>
                    </Table.Row>
                </Table.Header>

                <Table.Body>
                    {accounts.map(account => (
                        <Table.Row key={account.id}>
                            <Table.Cell>{account.name}</Table.Cell>
                            <Table.Cell>{account.assets.length}</Table.Cell>
                            <Table.Cell>
                                <Button as={NavLink} to={`/accounts/${account.id}`} primary inverted><Icon name='info circle' />Детали</Button>
                                <Button as={NavLink} to={`/accounts/${account.id}/deals`} primary inverted><Icon name='list' />Сделки</Button>
                                <Button onClick={() => modalStore.openModal(<AccountForm id={account.id} />)} primary inverted>Изменить</Button>
                            </Table.Cell>
                        </Table.Row>
                    ))}
                </Table.Body>

                <Table.Footer>
                    <Table.Row>
                        <Table.HeaderCell>Всего счетов</Table.HeaderCell>
                        <Table.HeaderCell>{accounts.length}</Table.HeaderCell>
                        <Table.HeaderCell>
                            <Button onClick={() => modalStore.openModal(<AccountForm id={''} />)} size="mini" positive>+</Button>
                        </Table.HeaderCell>
                    </Table.Row>
                </Table.Footer>
            </Table>
        </>

    )
})