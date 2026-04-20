import { describe, expect, it } from 'vitest';
import {
  getAuthErrorMessage,
  normalizeLoginInput,
  normalizeRegisterInput,
  validateRegisterPasswords,
} from './auth-domain';

describe('auth-domain', () => {
  it('normalizes login input', () => {
    expect(
      normalizeLoginInput({
        email: '  USER@example.com ',
        password: 'secret',
        rememberMe: true,
      }),
    ).toEqual({
      email: 'USER@example.com',
      password: 'secret',
      rememberMe: true,
    });
  });

  it('normalizes register input', () => {
    expect(
      normalizeRegisterInput({
        email: '  user@example.com ',
        username: '  demo-user ',
        password: 'StrongPass1',
      }),
    ).toEqual({
      email: 'user@example.com',
      username: 'demo-user',
      password: 'StrongPass1',
    });
  });

  it('parses validation and identity errors', () => {
    expect(
      getAuthErrorMessage(
        new Error(JSON.stringify({ errors: { email: ['Email taken'] } })),
        'fallback',
      ),
    ).toBe('Email taken');

    expect(
      getAuthErrorMessage(
        new Error(JSON.stringify([{ description: 'Password too weak' }])),
        'fallback',
      ),
    ).toBe('Password too weak');
  });

  it('falls back to generic messages and validates matching passwords', () => {
    expect(getAuthErrorMessage(new Error('plain'), 'fallback')).toBe('plain');
    expect(getAuthErrorMessage('unexpected', 'fallback')).toBe('fallback');
    expect(validateRegisterPasswords('a', 'b')).toBe('Пароли не совпадают.');
    expect(validateRegisterPasswords('a', 'a')).toBeNull();
  });
});
