import { observer } from "mobx-react-lite"
import React, { useEffect } from "react"
import { NavLink, useParams } from "react-router-dom";
import { Button, Icon, Table } from "semantic-ui-react"
import LoadingComponent from "../../app/layout/LoadingComponent";
import { useStore } from "../../app/store/store";

export default observer(function AccountList() {
    const { dealStore } = useStore();
    const { loadDeals, deals } = dealStore;
    const { id } = useParams<{ id: string }>();

    useEffect(() => {
        if (id) loadDeals(id);
    }, [id, loadDeals])

    if (dealStore.loadingInitial) return <LoadingComponent content='Loading accounts...' />

    return (
        <Table celled inverted>
            <Table.Header>
                <Table.Row>
                    <Table.HeaderCell>Дата</Table.HeaderCell>
                    <Table.HeaderCell>Тикер</Table.HeaderCell>
                    <Table.HeaderCell>Кол-во</Table.HeaderCell>
                    <Table.HeaderCell>Цена</Table.HeaderCell>
                    <Table.HeaderCell>Комиссия</Table.HeaderCell>
                    <Table.HeaderCell>Операция</Table.HeaderCell>
                    <Table.HeaderCell>Действие</Table.HeaderCell>
                </Table.Row>
            </Table.Header>


            <Table.Body>
                {deals.map(deal => (
                    <Table.Row key={deal.id}>
                        <Table.Cell>{deal.createdAt.toLocaleDateString()}</Table.Cell>
                        <Table.Cell>{deal.stock}</Table.Cell>
                        <Table.Cell>{deal.quantity}</Table.Cell>
                        <Table.Cell>{deal.price}</Table.Cell>
                        <Table.Cell>{deal.commission}</Table.Cell>
                        <Table.Cell>{deal.operation}</Table.Cell>
                        <Table.Cell>
                            {/* <Button as={NavLink} to={`/accounts/${account.id}/deals`} primary inverted><Icon name='list' />Сделки</Button> */}
                        </Table.Cell>
                    </Table.Row>
                ))}
            </Table.Body>
        </Table>
    )
})