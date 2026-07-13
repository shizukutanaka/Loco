import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App.tsx'
import { ToastProvider } from '@/contexts/ToastContext'
import { ErrorBoundary } from '@/components/ErrorBoundary/ErrorBoundary'
import { bootstrapSettings } from '@/config/appSettings'
import '@/styles/index.css'

// Restore the persisted API credential before the first request is made,
// so a reload does not silently drop authentication.
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
