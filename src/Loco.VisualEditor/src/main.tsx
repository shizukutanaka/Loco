import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App.tsx'
import { ToastProvider } from '@/contexts/ToastContext'
import { ErrorBoundary } from '@/components/ErrorBoundary/ErrorBoundary'
import '@/styles/index.css'

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <ErrorBoundary>
      <ToastProvider>
        <App />
      </ToastProvider>
    </ErrorBoundary>
  </React.StrictMode>,
)
