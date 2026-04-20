import { describe, expect, it } from 'vitest';
import { resolveAppViewState } from './app-view-state';

describe('app-view-state', () => {
  it('shows boot spinner while application is booting', () => {
    expect(resolveAppViewState({ booting: true, authLoading: false, hasUser: false })).toBe('booting');
  });

  it('keeps auth form mounted during login attempt failure path', () => {
    expect(resolveAppViewState({ booting: false, authLoading: true, hasUser: false })).toBe('auth');
  });

  it('shows dashboard when authenticated and no auth transition is active', () => {
    expect(resolveAppViewState({ booting: false, authLoading: false, hasUser: true })).toBe('dashboard');
  });

  it('shows transition spinner when authenticated session is changing', () => {
    expect(resolveAppViewState({ booting: false, authLoading: true, hasUser: true })).toBe('booting');
  });
});
