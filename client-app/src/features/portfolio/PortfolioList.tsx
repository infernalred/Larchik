import { observer } from "mobx-react-lite"
import React, { useEffect } from "react"
import { Table } from "semantic-ui-react"
import LoadingComponent from "../../app/layout/LoadingComponent"
import { useStore } from "../../app/store/store"

export default observer(function PortfolioList() {
    const { portfolioStore } = useStore();
    const { loadPortfolio, loadingPortfolio, portfolio } = portfolioStore;

    useEffect(() => {
        loadPortfolio();
    }, [loadPortfolio])

    if (loadingPortfolio) return <LoadingComponent content='Loading portfolio...' />

    return (
        <Table celled>
            <Table.Header>
                <Table.Row>
                    <Table.HeaderCell>Тикер</Table.HeaderCell>
                    <Table.HeaderCell>Компания</Table.HeaderCell>
                    <Table.HeaderCell>Сектор</Table.HeaderCell>
                    <Table.HeaderCell>Тип</Table.HeaderCell>
                    <Table.HeaderCell>Кол-во</Table.HeaderCell>
                    <Table.HeaderCell>Цена</Table.HeaderCell>
                    <Table.HeaderCell>Сумма рыночная</Table.HeaderCell>
                    <Table.HeaderCell>Средняя цена</Table.HeaderCell>
                    <Table.HeaderCell>Сумма</Table.HeaderCell>
                </Table.Row>
            </Table.Header>

            <Table.Body>
                {portfolio?.assets.map(asset => (
                    <Table.Row key={asset.ticker}>
                        <Table.Cell>{asset.ticker}</Table.Cell>
                        <Table.Cell>{asset.companyName}</Table.Cell>
                        <Table.Cell>{asset.sector}</Table.Cell>
                        <Table.Cell>{asset.type}</Table.Cell>
                        <Table.Cell>{asset.quantity}</Table.Cell>
                        <Table.Cell>{asset.price}</Table.Cell>
                        <Table.Cell>{asset.amount}</Table.Cell>
                        <Table.Cell>{asset.averagePrice}</Table.Cell>
                        <Table.Cell>{asset.amountByPurchase}</Table.Cell>
                    </Table.Row>
                ))}
            </Table.Body>
        </Table>
    )
})