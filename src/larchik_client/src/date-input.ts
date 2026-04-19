const DATE_ONLY_REGEX = /^\d{4}-\d{2}-\d{2}$/;

export function toDateInputValue(value?: string): string {
  return value ? value.slice(0, 10) : '';
}

export function toUtcIso(value?: string): string | undefined {
  if (!value) {
    return undefined;
  }

  if (DATE_ONLY_REGEX.test(value)) {
    return `${value}T00:00:00.000Z`;
  }

  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? undefined : parsed.toISOString();
}
