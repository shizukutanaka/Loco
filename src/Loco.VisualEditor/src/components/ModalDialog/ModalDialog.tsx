import { ReactNode, useRef, memo } from 'react';
import { X } from 'lucide-react';
import { useFocusTrap } from '@/hooks/useFocusTrap';

export interface ModalDialogProps {
  isOpen: boolean;
  onClose: () => void;
  title: string;
  children: ReactNode;
  footer?: ReactNode;
  showCloseButton?: boolean;
  overlayClassName?: string;
  containerClassName?: string;
  contentClassName?: string;
  maxWidth?: 'sm' | 'md' | 'lg' | 'xl' | '2xl';
  size?: 'small' | 'medium' | 'large';
}

const SIZES = {
  small: 'max-w-sm',
  medium: 'max-w-2xl',
  large: 'max-w-5xl',
} as const;

const MAX_WIDTHS = {
  sm: 'max-w-sm',
  md: 'max-w-md',
  lg: 'max-w-lg',
  xl: 'max-w-xl',
  '2xl': 'max-w-2xl',
} as const;

function ModalDialogComponent({
  isOpen,
  onClose,
  title,
  children,
  footer,
  showCloseButton = true,
  overlayClassName = 'fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4 sm:p-6',
  containerClassName = 'bg-white rounded-xl shadow-2xl flex flex-col',
  contentClassName = 'flex-1 overflow-y-auto p-4 sm:p-6',
  maxWidth = 'lg',
  size,
}: ModalDialogProps) {
  const modalRef = useRef<HTMLDivElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);

  useFocusTrap(modalRef, {
    isActive: isOpen,
    onEscape: onClose,
    restoreFocusRef: closeButtonRef,
  });

  const maxWidthClass = size ? SIZES[size] : MAX_WIDTHS[maxWidth];

  if (!isOpen) return null;

  return (
    <div
      className={overlayClassName}
      role="presentation"
      onClick={onClose}
    >
      <div
        ref={modalRef}
        className={`${containerClassName} ${maxWidthClass} w-full max-h-[90vh]`}
        role="dialog"
        aria-modal="true"
        aria-labelledby="modal-title"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="px-4 sm:px-6 py-4 border-b border-gray-200 flex items-center justify-between flex-shrink-0">
          <h2 id="modal-title" className="text-lg sm:text-xl font-bold text-gray-900">
            {title}
          </h2>
          {showCloseButton && (
            <button
              ref={closeButtonRef}
              onClick={onClose}
              className="p-2 text-gray-500 hover:bg-gray-100 rounded-lg transition-colors"
              aria-label={`Close ${title}`}
              title="Close"
            >
              <X className="w-5 h-5" aria-hidden="true" />
            </button>
          )}
        </div>

        <div className={contentClassName}>{children}</div>

        {footer && (
          <div className="px-4 sm:px-6 py-4 border-t border-gray-200 flex items-center gap-3 justify-end flex-shrink-0">
            {footer}
          </div>
        )}
      </div>
    </div>
  );
}

export const ModalDialog = memo(ModalDialogComponent);
ModalDialog.displayName = 'ModalDialog';
