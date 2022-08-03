import { observer } from "mobx-react-lite";
import React, { useCallback, useEffect, useState } from "react";
import { Link, useHistory, useLocation, useParams } from "react-router-dom";
import { Button, DropdownProps, Form, InputOnChangeData, Pagination, Segment, StrictPaginationProps, Table } from "semantic-ui-react";
import LoadingComponent from "../../app/layout/LoadingComponent";
import { PagingParams } from "../../app/models/pagination";
import { DealSearchParams } from "../../app/models/dealSearchParams";
import { useStore } from "../../app/store/store";

export default observer(function DealList() {
    const { dealStore, operationStore } = useStore();
    const { loadDeals, deals, loading, deleteDeal, pagination, pagingParams, setPagingParams, searchParams, setSearchParams, axiosParams } = dealStore;
    const { loadOperations, loadingOperations, operationsSet } = operationStore;
    const { id } = useParams<{ id: string }>();
    const [loadingNext, setLoadingNext] = useState(false);
    const history = useHistory()
    const { search } = useLocation();

    // const params = new URLSearchParams(search);
    // const pageNumber = Number(params.get("pageNumber") || 1);
    // setPagingParams(new PagingParams((pageNumber)));
    //const [pagingParams1, setPagingParams1] = useState<PagingParams>(new PagingParams(pageNumber));
    //const [searchParams, setSearchParams] = useState<DealSearchParams>(new DealSearchParams());
    const [timer, setTimer] = useState<NodeJS.Timeout | null>(null);

    //console.log(pagingParams1)


    function changeDelay() {
        if (timer) {
          clearTimeout(timer);
          setTimer(null);
        }

        setTimer(
            setTimeout(() => {
              console.log();
            }, 3000)
        );
    }

    function setPageLoad() {
        const params = new URLSearchParams(search);
        const pageNumber = Number(params.get("pageNumber") || 1);
        setPagingParams(new PagingParams((pageNumber)));
    }


    // function axiosParams1() {
    //     const params = new URLSearchParams();
    //     params.append("pageNumber", pagingParams1.pageNumber.toString());
    //     params.append("pageSize", pagingParams1.pageSize.toString());
    //     return params;
    // }

    // const axiosParams1 = useCallback(() => {
    //     const params = new URLSearchParams(search);
    //     const pageNumber = Number(params.get("pageNumber") || 1);
    //     params1.append("pageNumber", pagingParams1.pageNumber.toString());
    //     params1.append("pageSize", pagingParams1.pageSize.toString());

    //     if (searchParams.ticker) {
    //         params1.append("ticker", searchParams.ticker);
    //     }

    //     if (searchParams.operation) {
    //         params1.append("operation", searchParams.operation);
    //     }

    //     return params1;
    // }, [pagingParams1, searchParams])

    //const params1 = useMemo(() => axiosParams1(), [axiosParams1]);

    function handleGetNext(_: any, pageInfo: StrictPaginationProps) {
        setLoadingNext(true);
        //pagingParams1.pageNumber = (Number(pageInfo.activePage));
        //const paging = new PagingParams((Number(pageInfo.activePage)))
        //setPagingParams1(paging)
        setPagingParams(new PagingParams((Number(pageInfo.activePage))));
        loadDeals(id).then(() => setLoadingNext(false))
    }

    // function handleSearch(event: ChangeEvent<HTMLInputElement> | ChangeEvent<HTMLSelectElement>) {
    //     setLoadingNext(true);
    //     const {name, value} = event.target;
    //     console.log(name, value)
    //     setSearchParams({...searchParams, [name]: value})
    //     console.log(searchParams)
    // }

    function handleOnChangeDelay(_: any, data: InputOnChangeData | DropdownProps) {
        console.log("1")
        if (timer) {
            clearTimeout(timer);
            setTimer(null);
        }
        handleOnChange(_, data)
        setTimer(
            setTimeout(() => {
                
            }, 500)
        );        
    }

    function handleOnChange(_: any, data: InputOnChangeData | DropdownProps) {
        setLoadingNext(true);
        const {name, value} = data;
        setSearchParams({...searchParams, [name]: value})    
    }

    useEffect(() => {
        loadOperations();

        setPageLoad();

        history.push({ search: axiosParams.toString() })
        if (id) loadDeals(id);

    }, [loadOperations, id, loadDeals, axiosParams, history])

    if (dealStore.loadingDeals && !loadingNext) return <LoadingComponent content='Loading accounts...' />

    return (
        <>
            <Segment clearing>
                <Form autoComplete="off">
                    <Form.Group widths="equal">
                        <Form.Input fluid placeholder="Тикер" value={searchParams.ticker} name="ticker" onChange={handleOnChangeDelay} />
                        <Form.Select
                            fluid
                            options={operationsSet}
                            placeholder="Тип операции"
                            loading={loadingOperations}
                            value={searchParams.operation}
                            name="operation"
                            onChange={handleOnChange}
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