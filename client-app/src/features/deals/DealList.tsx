import { observer } from "mobx-react-lite";
import React, { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { Button, Form, Pagination, StrictPaginationProps, Table } from "semantic-ui-react";
import { number } from "yup";
import LoadingComponent from "../../app/layout/LoadingComponent";
import { PagingParams } from "../../app/models/pagination";
import { useStore } from "../../app/store/store";

export default observer(function AccountList() {
    const { dealStore, operationStore } = useStore();
    const { loadDeals, deals, loading, deleteDeal, pagination, setPagingParams } = dealStore;
    const { loadOperations, loadingOperations, operationsSet } = operationStore;
    const { id } = useParams<{ id: string }>();
    const [loadingNext, setLoadingNext] = useState(false);

    function handleGetNext(_: any, pageInfo: StrictPaginationProps) {
        setLoadingNext(true);
        setPagingParams(new PagingParams(Number(pageInfo.activePage)))
        loadDeals(id).then(() => setLoadingNext(false));
    }

    useEffect(() => {
        loadOperations();

        if (id) loadDeals(id);
    }, [loadOperations, id, loadDeals])

    if (dealStore.loadingDeals && !loadingNext) return <LoadingComponent content='Loading accounts...' />

    return (
        <>
            <Form>
                <Form.Group widths="equal">
                    <Form.Input fluid placeholder="Тикер" />
                    <Form.Select
                    fluid
                        options={operationsSet}
                        placeholder="Тип операции"
                        loading={loadingOperations}
                    />
                    <Pagination
                        boundaryRange={0}
                        ellipsisItem={null}
                        defaultActivePage={1}
                        totalPages={pagination ? pagination.totalPages : 0}
                        onPageChange={handleGetNext}
                    />
                </Form.Group>
            </Form>

            <Table celled>
                <Table.Header>
                    <Table.Row>
                        <Table.HeaderCell>Дата</Table.HeaderCell>
                        <Table.HeaderCell>Тикер</Table.HeaderCell>
                        <Table.HeaderCell>Кол-во</Table.HeaderCell>
                        <Table.HeaderCell>Цена</Table.HeaderCell>
                        <Table.HeaderCell>Комиссия</Table.HeaderCell>
                        <Table.HeaderCell>Тип операции</Table.HeaderCell>
                        <Table.HeaderCell>Действия</Table.HeaderCell>
                    </Table.Row>
                </Table.Header>


                <Table.Body>
                    {deals.map(deal => (
                        <Table.Row key={deal.id}>
                            <Table.Cell>{deal.createdAt.toLocaleDateString("ru")}</Table.Cell>
                            <Table.Cell>{deal.stock || deal.currency}</Table.Cell>
                            <Table.Cell>{deal.quantity}</Table.Cell>
                            <Table.Cell>{deal.price.toLocaleString("ru")}</Table.Cell>
                            <Table.Cell>{deal.commission.toLocaleString("ru")}</Table.Cell>
                            <Table.Cell>{deal.operation}</Table.Cell>
                            <Table.Cell>
                                <Button
                                    as={Link}
                                    to={`/deal/${deal.id}`}
                                    color="teal"
                                    content="Изменить" />
                                <Button
                                    color="red"
                                    content="Удалить"
                                    onClick={() => deleteDeal(deal.id)}
                                    loading={loading} />
                            </Table.Cell>
                        </Table.Row>
                    ))}
                </Table.Body>

                <Table.Footer>
                    <Table.Row>
                        <Table.HeaderCell>Всего сделок</Table.HeaderCell>
                        <Table.HeaderCell>{pagination?.totalItems}</Table.HeaderCell>
                        <Table.HeaderCell />
                        <Table.HeaderCell />
                        <Table.HeaderCell />
                        <Table.HeaderCell />
                        <Table.HeaderCell />
                    </Table.Row>
                </Table.Footer>
            </Table></>
    )
})