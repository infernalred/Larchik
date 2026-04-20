export type AuthMode = 'login' | 'register';

export interface LoginInput {
  email: string;
  password: string;
  rememberMe: boolean;
}

export interface RegisterInput {
  email: string;
  username: string;
  password: string;
}

export function normalizeLoginInput(input: LoginInput): LoginInput {
  return {
    ...input,
    email: input.email.trim(),
  };
}

export function normalizeRegisterInput(input: RegisterInput): RegisterInput {
  return {
    ...input,
    email: input.email.trim(),
    username: input.username.trim(),
  };
}

export function validateRegisterPasswords(password: string, confirmPassword: string): string | null {
  return password === confirmPassword ? null : 'Пароли не совпадают.';
}

export function getAuthErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof Error)) {
    return fallback;
  }

  try {
    const payload = JSON.parse(error.message) as
      | {
          title?: string;
          errors?: Record<string, string[]>;
          message?: string;
        }
      | Array<{ description?: string }>;

    if (Array.isArray(payload)) {
      const identityError = payload.map((item) => item.description).find(Boolean);
      return identityError ?? error.message ?? fallback;
    }

    const validationError = payload.errors
      ? Object.values(payload.errors)
          .flat()
          .find(Boolean)
      : undefined;

    return validationError ?? payload.message ?? payload.title ?? error.message ?? fallback;
  } catch {
    return error.message || fallback;
  }
}
