import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App.tsx'
import { ToastProvider } from '@/contexts/ToastContext'
import { ErrorBoundary } from '@/components/ErrorBoundary/ErrorBoundary'
import { bootstrapSettings } from '@/config/appSettings'
import '@/styles/index.css'

// Re-apply a stored bearer token before the first request is made, so a
// reload does not drop the session. App shows the sign-in dialog when there
// is no usable token.
bootstrapSettings()

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <ErrorBoundary>
      <ToastProvider>
        <App />
      </ToastProvider>
    </ErrorBoundary>
  </React.StrictMode>,
)
