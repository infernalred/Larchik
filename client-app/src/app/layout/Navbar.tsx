import { observer } from 'mobx-react-lite';
import React from 'react';
import { NavLink, Link } from 'react-router-dom';
import { Menu, Container, Button, Dropdown } from 'semantic-ui-react';
import LoginForm from '../../features/users/LoginForm';
import RegisterForm from '../../features/users/RegisterForm';
import { useStore } from '../store/store';

export default observer(function Navbar() {
    const { userStore, modalStore } = useStore();
    const { user, isLoggedIn, logout } = userStore;

    return (
        <Menu inverted fixed='top'>
            <Container>
                <Menu.Item as={NavLink} to='/' exact header>
                    <img src='/assets/logo.png' alt='logo' style={{ marginRight: '10px' }}/>
                    Larchik
                </Menu.Item>
                {isLoggedIn &&
                    <Menu.Item>
                        <Button as={NavLink} to='/accounts' positive content='Счета' />
                        <Button as={NavLink} to='/deals' positive content='Сделки' />
                        <Button as={NavLink} to='/reports' positive content='Отчеты' />
                    </Menu.Item>
                    
                }
                {isLoggedIn ? (
                    <Menu.Item position='right'>
                        <Dropdown item text={user?.displayName}>
                            <Dropdown.Menu>
                                <Dropdown.Item as={Link} to={'/user'} text='Профиль' icon='user' />
                                <Dropdown.Item onClick={logout} text='Выйти' icon='power' />
                            </Dropdown.Menu>
                        </Dropdown>
                    </Menu.Item>

                ) : (
                    <Menu.Item position='right'>
                        <Button onClick={() => modalStore.openModal(<LoginForm />)} size='huge' positive>
                            Войти
                        </Button>
                        <Button onClick={() => modalStore.openModal(<RegisterForm />)} size='huge' positive>
                            Регистрация
                        </Button>
                    </Menu.Item>
                )}
            </Container>
        </Menu>
    )
})