/**
 * FormGroup Component
 *
 * Wrapper component for grouping related form fields.
 * Provides consistent spacing and layout for form sections.
 */

import { memo, ReactNode } from 'react';

export interface FormGroupProps {
  /** Group title/heading */
  title?: string;
  /** Group description */
  description?: string;
  /** Form fields/content */
  children: ReactNode;
  /** Custom CSS class */
  className?: string;
  /** Space between fields */
  spacing?: 'sm' | 'md' | 'lg';
  /** Add bottom border separator */
  divider?: boolean;
}

const spacingClasses = {
  sm: 'space-y-2',
  md: 'space-y-4',
  lg: 'space-y-6',
};

export const FormGroup = memo(
  ({
    title,
    description,
    children,
    className = '',
    spacing = 'md',
    divider = false,
  }: FormGroupProps) => {
    return (
      <div
        className={`
          ${divider ? 'pb-6 border-b border-gray-200' : 'pb-4'}
          ${className}
        `}
      >
        {(title || description) && (
          <div className="mb-4">
            {title && (
              <h3 className="text-sm font-semibold text-gray-700 mb-1">
                {title}
              </h3>
            )}
            {description && (
              <p className="text-xs text-gray-500">{description}</p>
            )}
          </div>
        )}
        <div className={spacingClasses[spacing]}>
          {children}
        </div>
      </div>
    );
  }
);

FormGroup.displayName = 'FormGroup';
