import { observer } from "mobx-react-lite"
import React, { useEffect } from "react"
import { Table } from "semantic-ui-react"
import LoadingComponent from "../../app/layout/LoadingComponent";
import { useStore } from "../../app/store/store";

export default observer(function AccountList() {
    const { accountStore } = useStore();
    const { loadAccounts, accounts } = accountStore;

    useEffect(() => {
        if (accounts.length <= 1) loadAccounts();
    }, [accounts.length, loadAccounts])

    if (accountStore.loadingAccounts) return <LoadingComponent content='Loading accounts...' />

    return (
        <Table celled>
            <Table.Header>
                <Table.Row>
                    <Table.HeaderCell>Тикер</Table.HeaderCell>
                    <Table.HeaderCell>Компания</Table.HeaderCell>
                    <Table.HeaderCell>Категория</Table.HeaderCell>
                    <Table.HeaderCell>Кол-во</Table.HeaderCell>
                </Table.Row>
            </Table.Header>

            {accounts.map(account => (
            <Table.Body key={account.id}>
                {account.assets.map(asset => (
                    <Table.Row key={asset.id}>
                        <Table.Cell>{asset.stock.ticker}</Table.Cell>
                        <Table.Cell>{asset.stock.companyName}</Table.Cell>
                        <Table.Cell>{asset.stock.sector}</Table.Cell>
                        <Table.Cell>{asset.quantity}</Table.Cell>
                    </Table.Row>
                ))}
                    
                

            </Table.Body>
            ))}
        </Table>
    )
})