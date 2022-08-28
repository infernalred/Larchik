import { observer } from "mobx-react-lite";
import React, { useEffect, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { Button, DropdownProps, Form, InputOnChangeData, Pagination, Segment, StrictPaginationProps, Table } from "semantic-ui-react";
import LoadingComponent from "../../app/layout/LoadingComponent";
import { PagingParams } from "../../app/models/pagination";
import { DealSearchParams } from "../../app/models/dealSearchParams";
import { useStore } from "../../app/store/store";

export default observer(function DealList() {
    const { dealStore, dealTypeStore } = useStore();
    const { loadDeals, deals, loading, deleteDeal, pagination, pagingParams, setPagingParams, dealSearchParams, setDealSearchParams } = dealStore;
    const { loadDealTypes, loadingDealTypes, dealTypesSet, dealTypesRegistry } = dealTypeStore;
    const { id } = useParams<{ id: string }>();
    const [loadingNext, setLoadingNext] = useState(false);
    const [searchParams, setSearchParams] = useSearchParams();
    const [pageNumber] = useState(Number(searchParams.get("pageNumber")) || pagingParams.pageNumber);
    const [pageSize] = useState(Number(searchParams.get("pageSize") || pagingParams.pageSize));
    const [ticker] = useState(searchParams.get("ticker") || "");
    const [type] = useState(Number(searchParams.get("type")) || undefined);
    const [timer, setTimer] = useState<NodeJS.Timeout | null>(null);

    function getParams(paging: PagingParams, searchParams: DealSearchParams) {
        const params = new URLSearchParams();
        params.append("pageNumber", paging.pageNumber.toString());
        params.append("pageSize", paging.pageSize.toString());

        if (searchParams.ticker) {
            params.append("ticker", searchParams.ticker);
        }

        if (searchParams.type) {
            params.append("type", searchParams.type.toString());
        }
        return params;
    }

    function handleGetNext(_: any, pageInfo: StrictPaginationProps) {
        setLoadingNext(true);
        const newPagingParams = new PagingParams(Number(pageInfo.activePage), pagingParams.pageSize)
        setPagingParams(newPagingParams);
        setSearchParams(getParams(newPagingParams, dealSearchParams));
        loadDeals(id!).then(() => setLoadingNext(false));
    }

    function handleOnChangeType(_: any, data: DropdownProps) {
        setLoadingNext(true);
        handleOnChange(dealSearchParams.ticker, Number(data.value) || undefined);
        loadDeals(id!).then(() => setLoadingNext(false));
    }

    function handleOnChangeDelay(_: any, data: InputOnChangeData) {
        if (timer) {
            clearTimeout(timer);
            setTimer(null);
        }

        handleOnChange(data.value, dealSearchParams.type)
        setLoadingNext(true);
        setTimer(
            setTimeout(() => {
                loadDeals(id!).then(() => setLoadingNext(false));
            }, 500)
        );
    }

    function handleOnChange(ticker: string, type: number | undefined = undefined) {        
        const newDealSearchParams = new DealSearchParams(ticker, type);
        setDealSearchParams(newDealSearchParams);
        setSearchParams(getParams(pagingParams, newDealSearchParams));
    }

    useEffect(() => {
        if (id) {
            setPagingParams(new PagingParams(pageNumber, pageSize));
            setDealSearchParams(new DealSearchParams(ticker, type));
            loadDeals(id);
            loadDealTypes();
        }

    }, [id, loadDeals, dealTypesSet.length, loadDealTypes, pageNumber, pageSize, setPagingParams, setDealSearchParams, ticker, type])

    if (dealStore.loadingDeals && !loadingNext) return <LoadingComponent content='Loading accounts...' />

    return (
        <>
            <Segment clearing>
                <Form autoComplete="off">
                    <Form.Group widths="equal">
                        <Form.Input fluid placeholder="Тикер" value={dealSearchParams.ticker} name="ticker" onChange={handleOnChangeDelay} />
                        <Form.Select
                            fluid
                            options={dealTypesSet}
                            placeholder="Тип сделки"
                            loading={loadingDealTypes}
                            value={dealSearchParams.type}
                            name="type"
                            onChange={handleOnChangeType}
                            clearable
                        />
                        <Pagination
                            boundaryRange={0}
                            ellipsisItem={null}
                            defaultActivePage={pagingParams.pageNumber}
                            totalPages={pagination ? pagination.totalPages : 0}
                            onPageChange={handleGetNext}
                        />
                    </Form.Group>
                </Form>
            </Segment>
            <Table celled>
                <Table.Header>
                    <Table.Row>
                        <Table.HeaderCell>Дата</Table.HeaderCell>
                        <Table.HeaderCell>Тикер</Table.HeaderCell>
                        <Table.HeaderCell>Кол-во</Table.HeaderCell>
                        <Table.HeaderCell>Цена</Table.HeaderCell>
                        <Table.HeaderCell>Комиссия</Table.HeaderCell>
                        <Table.HeaderCell>Тип сделки</Table.HeaderCell>
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
                            <Table.Cell>{deal.type ? dealTypesRegistry.get(deal.type.toString())?.code : ""}</Table.Cell>
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
