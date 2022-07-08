import { observer } from "mobx-react-lite";
import React, { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useHistory, useLocation, useParams } from "react-router-dom";
import { Button, Form, Pagination, StrictPaginationProps, Table } from "semantic-ui-react";
import LoadingComponent from "../../app/layout/LoadingComponent";
import { PagingParams } from "../../app/models/pagination";
import { useStore } from "../../app/store/store";

export default observer(function AccountList() {
    const { dealStore, operationStore } = useStore();
    const { loadDeals, deals, loading, deleteDeal, pagination } = dealStore;
    const { loadOperations, loadingOperations, operationsSet } = operationStore;
    const { id } = useParams<{ id: string }>();
    const [loadingNext, setLoadingNext] = useState(false);
    const history = useHistory()
    const { search } = useLocation();

    const params = new URLSearchParams(search);
    const pageNumber = Number(params.get("pageNumber") || 1);
    const [pagingParams1, setPagingParams1] = useState<PagingParams>(new PagingParams(pageNumber));
    console.log(pagingParams1)
    

    

    // function axiosParams1() {
    //     const params = new URLSearchParams();
    //     params.append("pageNumber", pagingParams1.pageNumber.toString());
    //     params.append("pageSize", pagingParams1.pageSize.toString());
    //     return params;
    // }

    const axiosParams1 = useCallback(() =>  {
        const params1 = new URLSearchParams();
        params1.append("pageNumber", pagingParams1.pageNumber.toString());
        params1.append("pageSize", pagingParams1.pageSize.toString());
        return params1;
    }, [pagingParams1])

    const params1 = useMemo(() => axiosParams1(), [axiosParams1]);

    function handleGetNext(_: any, pageInfo: StrictPaginationProps) {
        setLoadingNext(true);
        pagingParams1.pageNumber = (Number(pageInfo.activePage));
        const paging = new PagingParams((Number(pageInfo.activePage)))
        setPagingParams1(paging)
    }

    useEffect(() => {
        loadOperations();
                
        history.push({search: params1.toString()})
        if (id) loadDeals(id, params1);
        
    }, [loadOperations, id, loadDeals, pagingParams1, params1, history])

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
                        defaultActivePage={pagingParams1.pageNumber}
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