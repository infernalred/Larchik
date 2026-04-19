import { useMemo, useRef, useState, type ChangeEvent } from 'react';
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import { resolveImportMaxFileSizeMb, validateImportFile } from './import-operations-domain';

interface Props {
  open: boolean;
  submitting: boolean;
  canSubmit: boolean;
  disabledReason?: string;
  error?: string;
  onClose: () => void;
  onSubmit: (file: File) => Promise<void>;
}

const ACCEPT_ATTR = '.xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';
const maxFileSizeMb = resolveImportMaxFileSizeMb(import.meta.env.VITE_IMPORT_MAX_FILE_SIZE_MB);

export function ImportOperationsDialog({
  open,
  submitting,
  canSubmit,
  disabledReason,
  error,
  onClose,
  onSubmit,
}: Props) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'));
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [validationError, setValidationError] = useState('');

  const handleEnter = () => {
    setFile(null);
    setValidationError('');

    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  const hasSelectedFile = useMemo(() => file !== null && !submitting, [file, submitting]);

  const handleFileChange = (event: ChangeEvent<HTMLInputElement>) => {
    const selected = event.target.files?.[0] ?? null;
    const nextError = validateImportFile(selected, maxFileSizeMb);
    if (nextError) {
      setFile(null);
      setValidationError(nextError);
      event.target.value = '';
      return;
    }

    setFile(selected);
    setValidationError('');
  };

  const handleSubmit = async () => {
    const selectedFile = file;
    const nextError = validateImportFile(selectedFile, maxFileSizeMb);
    if (nextError) {
      setValidationError(nextError);
      return;
    }

    if (!selectedFile) {
      return;
    }

    await onSubmit(selectedFile);
  };

  return (
    <Dialog
      open={open}
      onClose={submitting ? undefined : onClose}
      fullWidth
      maxWidth="sm"
      fullScreen={isMobile}
      scroll="paper"
      slotProps={{ transition: { onEnter: handleEnter } }}
    >
      <DialogTitle>Импорт операций из отчета</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Alert severity="info">
            {canSubmit
              ? 'Загрузите исходный XLSX-файл отчета для выбранного брокера.'
              : disabledReason ?? 'Для выбранного брокера импорт пока не настроен.'}
          </Alert>
          {error && <Alert severity="error">{error}</Alert>}
          {validationError && <Alert severity="warning">{validationError}</Alert>}

          <Stack spacing={1}>
            <Button
              variant="outlined"
              component="label"
              disabled={submitting || !canSubmit}
              sx={{ textTransform: 'none', alignSelf: 'flex-start' }}
            >
              Выбрать файл
              <input
                ref={fileInputRef}
                hidden
                type="file"
                accept={ACCEPT_ATTR}
                onChange={handleFileChange}
              />
            </Button>
            <Typography variant="body2" color={file ? 'text.primary' : 'text.secondary'}>
              {file ? file.name : 'Файл не выбран'}
            </Typography>
          </Stack>
        </Stack>
      </DialogContent>
      <DialogActions
        sx={{
          px: { xs: 2, sm: 3 },
          pb: { xs: 2, sm: 1.5 },
          pt: 1,
          flexDirection: { xs: 'column', sm: 'row' },
          gap: 1,
        }}
      >
        <Button onClick={onClose} disabled={submitting} fullWidth={isMobile}>
          Отмена
        </Button>
        <Button
          onClick={handleSubmit}
          variant="contained"
          disabled={!canSubmit || !hasSelectedFile}
          fullWidth={isMobile}
        >
          {submitting ? 'Импортируем…' : 'Импортировать'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
