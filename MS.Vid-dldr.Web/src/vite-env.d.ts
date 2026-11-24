/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Manager origin for the built app, e.g. http://localhost:8000. Leave empty to use the same origin. */
  readonly VITE_MANAGER_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
