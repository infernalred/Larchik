/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE?: string;
  readonly VITE_IMPORT_MAX_FILE_SIZE_MB?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
