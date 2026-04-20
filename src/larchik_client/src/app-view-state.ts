export type AppViewState = 'booting' | 'auth' | 'dashboard';

export interface AppViewStateInput {
  booting: boolean;
  authLoading: boolean;
  hasUser: boolean;
}

export function resolveAppViewState({ booting, authLoading, hasUser }: AppViewStateInput): AppViewState {
  if (booting) {
    return 'booting';
  }

  if (!hasUser) {
    return 'auth';
  }

  return authLoading ? 'booting' : 'dashboard';
}
