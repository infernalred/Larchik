import { observer } from "mobx-react-lite";
import { Grid, Table } from "semantic-ui-react";
import Calendar from 'react-calendar';

export default observer(function CurrencyReport() {
    return (
        <Grid>
            <Grid.Column width='10'>
            <Table celled inverted>
                <Table.Header>
                    <Table.Row>
                        <Table.HeaderCell>Валюта</Table.HeaderCell>
                        <Table.HeaderCell>Операция</Table.HeaderCell>
                        <Table.HeaderCell>Сумма</Table.HeaderCell>
                    </Table.Row>
                </Table.Header>

                <Table.Body>
                    
                </Table.Body>
            </Table>
            </Grid.Column>
            <Grid.Column width='6'>
            <Calendar 
                onChange={(date: any) => console.log(date)}
                value={new Date()}
            />
            <Calendar 
                onChange={(date: any) => console.log(date)}
                value={new Date()}
            />
            </Grid.Column>
        </Grid>
    )
})