export function getApiErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof Error)) {
    return fallback;
  }

  try {
    const payload = JSON.parse(error.message) as { message?: string };
    return payload.message || fallback;
  } catch {
    return error.message || fallback;
  }
}
