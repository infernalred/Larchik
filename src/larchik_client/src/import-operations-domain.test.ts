import { describe, expect, it } from 'vitest';
import { resolveImportMaxFileSizeMb, validateImportFile } from './import-operations-domain';

describe('import-operations-domain', () => {
  it('resolves max file size from env or fallback', () => {
    expect(resolveImportMaxFileSizeMb('25')).toBe(25);
    expect(resolveImportMaxFileSizeMb('0')).toBe(10);
    expect(resolveImportMaxFileSizeMb('bad')).toBe(10);
    expect(resolveImportMaxFileSizeMb(undefined)).toBe(10);
  });

  it('validates selected import file', () => {
    expect(validateImportFile(null, 10)).toBe('Выберите файл отчета.');
    expect(validateImportFile({ name: 'report.xlsx', size: 0 }, 10)).toBe('Нельзя загрузить пустой файл.');
    expect(validateImportFile({ name: 'report.csv', size: 100 }, 10)).toBe('Поддерживаются только файлы .xlsx.');
    expect(validateImportFile({ name: 'report.xlsx', size: 11 * 1024 * 1024 }, 10)).toBe(
      'Файл отчета слишком большой. Максимальный размер 10 MB.',
    );
    expect(validateImportFile({ name: 'report.xlsx', size: 100 }, 10)).toBeNull();
  });
});
