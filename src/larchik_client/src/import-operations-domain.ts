const DEFAULT_MAX_FILE_SIZE_MB = 10;

export interface ImportableFileLike {
  name: string;
  size: number;
}

export function resolveImportMaxFileSizeMb(raw: string | undefined): number {
  const parsed = raw ? Number.parseInt(raw, 10) : Number.NaN;
  return Number.isFinite(parsed) && parsed > 0 ? parsed : DEFAULT_MAX_FILE_SIZE_MB;
}

export function validateImportFile(file: ImportableFileLike | null, maxFileSizeMb: number): string | null {
  if (!file) {
    return 'Выберите файл отчета.';
  }

  if (file.size === 0) {
    return 'Нельзя загрузить пустой файл.';
  }

  if (file.size > maxFileSizeMb * 1024 * 1024) {
    return `Файл отчета слишком большой. Максимальный размер ${maxFileSizeMb} MB.`;
  }

  if (!file.name.toLowerCase().endsWith('.xlsx')) {
    return 'Поддерживаются только файлы .xlsx.';
  }

  return null;
}
