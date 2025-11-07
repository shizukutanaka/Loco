/**
 * Toast Component
 *
 * Displays toast notifications in the bottom-right corner of the screen.
 * Automatically disappears after a specified duration.
 */

import { useEffect, useState } from 'react';
import { X, CheckCircle, XCircle, Info, AlertTriangle } from 'lucide-react';
import { useToast, Toast as ToastType } from '@/contexts/ToastContext';

// ============================================================================
// Toast Item Component
// ============================================================================

interface ToastItemProps {
  toast: ToastType;
  onClose: () => void;
}

function ToastItem({ toast, onClose }: ToastItemProps) {
  const [isVisible, setIsVisible] = useState(false);

  useEffect(() => {
    // Trigger animation after mount
    setTimeout(() => setIsVisible(true), 10);
  }, []);

  const handleClose = () => {
    setIsVisible(false);
    // Wait for animation before removing
    setTimeout(onClose, 300);
  };

  // Icon based on toast type
  const getIcon = () => {
    switch (toast.type) {
      case 'success':
        return <CheckCircle className="w-5 h-5 text-green-500" />;
      case 'error':
        return <XCircle className="w-5 h-5 text-red-500" />;
      case 'warning':
        return <AlertTriangle className="w-5 h-5 text-yellow-500" />;
      case 'info':
      default:
        return <Info className="w-5 h-5 text-blue-500" />;
    }
  };

  // Background color based on toast type
  const getBackgroundClass = () => {
    switch (toast.type) {
      case 'success':
        return 'bg-green-50 border-green-200';
      case 'error':
        return 'bg-red-50 border-red-200';
      case 'warning':
        return 'bg-yellow-50 border-yellow-200';
      case 'info':
      default:
        return 'bg-blue-50 border-blue-200';
    }
  };

  return (
    <div
      className={`flex items-start gap-3 p-4 rounded-lg border shadow-lg transition-all duration-300 min-w-[300px] max-w-[400px] ${getBackgroundClass()} ${
        isVisible ? 'opacity-100 translate-x-0' : 'opacity-0 translate-x-full'
      }`}
    >
      {getIcon()}
      <div className="flex-1">
        <p className="text-sm text-gray-900">{toast.message}</p>
      </div>
      <button
        onClick={handleClose}
        className="p-1 hover:bg-gray-200 rounded transition-colors"
        aria-label="Close toast"
      >
        <X className="w-4 h-4 text-gray-500" />
      </button>
    </div>
  );
}

// ============================================================================
// Toast Container Component
// ============================================================================

export function ToastContainer() {
  const { toasts, removeToast } = useToast();

  return (
    <div className="fixed bottom-6 right-6 z-[9999] flex flex-col gap-3">
      {toasts.map((toast) => (
        <ToastItem key={toast.id} toast={toast} onClose={() => removeToast(toast.id)} />
      ))}
    </div>
  );
}
