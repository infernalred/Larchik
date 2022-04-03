import { observer } from "mobx-react-lite"
import React, { useEffect } from "react"
import { Link, useParams } from "react-router-dom";
import { Button, Table } from "semantic-ui-react"
import LoadingComponent from "../../app/layout/LoadingComponent";
import { useStore } from "../../app/store/store";

export default observer(function AccountList() {
    const { dealStore } = useStore();
    const { loadDeals, deals, loading, deleteDeal } = dealStore;
    const { id } = useParams<{ id: string }>();

    useEffect(() => {
        if (id) loadDeals(id);
    }, [id, loadDeals])

    if (dealStore.loadingDeals) return <LoadingComponent content='Loading accounts...' />

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
                            <Button
                                as={Link}
                                to={`/deal/${deal.id}`}
                                color="teal"
                                content="Изменить"
                            />
                            <Button
                                color="red"
                                content="Удалить"
                                onClick={() => deleteDeal(deal.id)}
                                loading={loading}
                            />
                        </Table.Cell>
                    </Table.Row>
                ))}
            </Table.Body>

            <Table.Footer>
                <Table.Row>
                    <Table.HeaderCell>Всего сделок</Table.HeaderCell>
                    <Table.HeaderCell>{deals.length}</Table.HeaderCell>
                </Table.Row>
            </Table.Footer>
        </Table>
    )
})