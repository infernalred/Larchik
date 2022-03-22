import React from 'react'
import { Link } from 'react-router-dom'
import { Segment, Header, Icon, Button } from 'semantic-ui-react'

export default function NotFound() {
    return (
        <Segment placeholder>
            <Header>
                <Icon name='search' />
                Ooops - we've looked everywhere and could not find this.
            </Header>
            <Segment.Inline>
                <Button as={Link} to='/' primary>
                    Return to start page
                </Button>
            </Segment.Inline>
        </Segment>
    )
}
