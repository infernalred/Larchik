import { observer } from "mobx-react-lite";
import React, { useEffect } from "react";
import { useParams } from "react-router-dom";
import { Table } from "semantic-ui-react";
import LoadingComponent from "../../app/layout/LoadingComponent";
import { useStore } from "../../app/store/store";

export default observer(function AccountDetails() {
    const { accountStore } = useStore();
    const { selectedAccount: account, loadAccount, loadingInitial } = accountStore;
    const { id } = useParams<{ id: string }>();

    useEffect(() => {
        if (id) loadAccount(id);
    }, [id, loadAccount]);

    if (loadingInitial || !account) return <LoadingComponent />;

    return (
        <Table celled>
            <Table.Header>
                <Table.Row>
                    <Table.HeaderCell>Название</Table.HeaderCell>
                    <Table.HeaderCell>Тикер</Table.HeaderCell>
                    <Table.HeaderCell>Кол-во</Table.HeaderCell>
                    <Table.HeaderCell>Сектор</Table.HeaderCell>
                    <Table.HeaderCell>Тип</Table.HeaderCell>
                </Table.Row>
            </Table.Header>


            <Table.Body>
                {account.assets.map(asset => (
                    <Table.Row key={asset.id}>
                        <Table.Cell width={3}>{asset.stock.companyName}</Table.Cell>
                        <Table.Cell>{asset.stock.ticker}</Table.Cell>
                        <Table.Cell>{asset.quantity.toLocaleString("ru")}</Table.Cell>
                        <Table.Cell>{asset.stock.sector}</Table.Cell>
                        <Table.Cell>{asset.stock.type}</Table.Cell>
                    </Table.Row>
                ))}
            </Table.Body>
            <Table.Footer>
                <Table.Row>
                    <Table.HeaderCell>Всего активов</Table.HeaderCell>
                    <Table.HeaderCell>{account.assets.length}</Table.HeaderCell>
                </Table.Row>
            </Table.Footer>
        </Table>
    )
})