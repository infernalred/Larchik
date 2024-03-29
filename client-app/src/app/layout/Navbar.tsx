import { observer } from 'mobx-react-lite';
import React from 'react';
import { NavLink, Link, useNavigate } from 'react-router-dom';
import { Menu, Container, Button, Dropdown } from 'semantic-ui-react';
import { useStore } from '../store/store';

export default observer(function Navbar() {
    const { userStore } = useStore();
    const { user, isLoggedIn, logout } = userStore;
    const navigate = useNavigate();

    function handleLogout() {
        logout();
        navigate("/")
    }

    return (
        <Menu inverted fixed='top'>
            <Container>
                <Menu.Item as={NavLink} to='/' header>
                    <img src='/assets/logo.png' alt='logo' style={{ marginRight: '10px' }} />
                    Larchik
                </Menu.Item>
                {isLoggedIn &&
                    <>
                        <Menu.Item as={NavLink} to='/accounts' content='Счета' />
                        <Menu.Item as={NavLink} to='/reports' content='Отчеты' />
                        <Menu.Item as={NavLink} to='/portfolio' content='Портфолио' />
                        <Menu.Item>
                            <Button as={NavLink} to='/deal' positive content='Новая сделка' />
                        </Menu.Item>
                        <Menu.Item position='right'>
                            <Dropdown item text={user?.displayName}>
                                <Dropdown.Menu>
                                    <Dropdown.Item as={Link} to={'/user'} text='Профиль' icon='user' />
                                    <Dropdown.Item onClick={handleLogout} text='Выйти' icon='power' />
                                </Dropdown.Menu>
                            </Dropdown>
                        </Menu.Item>
                    </>}
            </Container>
        </Menu>
    )
})