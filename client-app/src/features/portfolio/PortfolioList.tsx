import { observer } from "mobx-react-lite"
import React, { useEffect } from "react"
import { useParams } from "react-router-dom"
import { Table } from "semantic-ui-react"
import LoadingComponent from "../../app/layout/LoadingComponent"
import { PortfolioAsset } from "../../app/models/portfolioAsset"
import { useStore } from "../../app/store/store"

export default observer(function PortfolioList() {
    const { portfolioStore } = useStore();
    const { loadPortfolio, loadAccountPortfolio, loadingPortfolio, portfolio } = portfolioStore;
    const { id } = useParams<{ id: string }>();

    useEffect(() => {
        if (id) {
            loadAccountPortfolio(id);
        } else {
            loadPortfolio();
        }
        
    }, [id, loadAccountPortfolio, loadPortfolio])

    function getColor(asset: PortfolioAsset) {
        if (asset.averagePrice < asset.price) {
            return "greenstock";
        }

        if (asset.averagePrice > asset.price) {
            return "redstock";
        }
        
        return "";
    }

    if (loadingPortfolio) return <LoadingComponent content='Loading portfolio...' />

    return (
        <Table celled>
            <Table.Header>
                <Table.Row>
                    <Table.HeaderCell>Тикер</Table.HeaderCell>
                    <Table.HeaderCell width={2}>Компания</Table.HeaderCell>
                    <Table.HeaderCell>Сектор</Table.HeaderCell>
                    <Table.HeaderCell>Тип</Table.HeaderCell>
                    <Table.HeaderCell>Кол-во</Table.HeaderCell>
                    <Table.HeaderCell>Средняя цена</Table.HeaderCell>
                    <Table.HeaderCell>Цена</Table.HeaderCell>
                    <Table.HeaderCell>Стоимость актива</Table.HeaderCell>
                    <Table.HeaderCell>Сумма RUB</Table.HeaderCell>
                    <Table.HeaderCell>Доход</Table.HeaderCell>
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
                        <Table.Cell>{asset.averagePrice.toLocaleString("ru")}</Table.Cell>
                        <Table.Cell>{asset.price.toLocaleString("ru")}</Table.Cell>
                        <Table.Cell className={getColor(asset)}>{asset.amountMarket.toLocaleString("ru")}</Table.Cell>
                        <Table.Cell className={getColor(asset)}>{asset.amountMarketCurrency.toLocaleString("ru")}</Table.Cell>
                        <Table.Cell className={getColor(asset)}>{asset.profit.toLocaleString("ru")}</Table.Cell>
                    </Table.Row>
                ))}
            </Table.Body>

            <Table.Footer>
                <Table.Row>
                    <Table.HeaderCell />
                    <Table.HeaderCell />
                    <Table.HeaderCell />
                    <Table.HeaderCell />
                    <Table.HeaderCell />
                    <Table.HeaderCell>Стоимость</Table.HeaderCell>
                    <Table.HeaderCell>{portfolio?.totalBalance.toLocaleString("ru")}</Table.HeaderCell>
                    <Table.HeaderCell>Прибыль</Table.HeaderCell>
                    <Table.HeaderCell className={portfolio && portfolio?.profit < 0 ? "redstock" : "greenstock"}>{portfolio?.profit.toLocaleString("ru")}</Table.HeaderCell>
                    <Table.HeaderCell />
                </Table.Row>
            </Table.Footer>
        </Table>
    )
})