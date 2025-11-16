/**
 * Input Validation Utilities
 *
 * Provides safe validation and sanitization for user inputs following
 * OWASP security guidelines and modern best practices.
 */

/**
 * Validates if a URL is safe to use as an href attribute
 * Prevents javascript: and data: scheme attacks
 *
 * @param url - URL to validate
 * @returns true if URL is safe, false otherwise
 */
export function isSafeUrl(url: string | null | undefined): boolean {
  if (!url) return false;

  try {
    const trimmedUrl = url.trim().toLowerCase();

    // Block dangerous protocols
    if (
      trimmedUrl.startsWith('javascript:') ||
      trimmedUrl.startsWith('data:') ||
      trimmedUrl.startsWith('vbscript:')
    ) {
      return false;
    }

    // Allow relative URLs
    if (trimmedUrl.startsWith('/') || trimmedUrl.startsWith('#')) {
      return true;
    }

    // Validate absolute URLs
    const urlObj = new URL(url);
    return (
      urlObj.protocol === 'http:' ||
      urlObj.protocol === 'https:' ||
      urlObj.protocol === 'mailto:'
    );
  } catch {
    // Invalid URL format
    return false;
  }
}

/**
 * Validates email address format
 * Uses simple but practical regex pattern
 *
 * @param email - Email address to validate
 * @returns true if email format is valid
 */
export function isValidEmail(email: string): boolean {
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  return emailRegex.test(email.trim());
}

/**
 * Validates workflow name (alphanumeric, spaces, hyphens)
 * Prevents injection attacks in filenames
 *
 * @param name - Workflow name to validate
 * @returns true if name is valid
 */
export function isValidWorkflowName(name: string): boolean {
  if (!name || name.length === 0 || name.length > 255) {
    return false;
  }

  // Allow alphanumeric, spaces, hyphens, underscores, parentheses
  const validNameRegex = /^[a-zA-Z0-9\s\-_()]+$/;
  return validNameRegex.test(name.trim());
}

/**
 * Sanitizes user input to prevent XSS attacks
 * Escapes HTML special characters
 *
 * @param input - User input to sanitize
 * @returns Sanitized string safe to display in HTML
 */
export function sanitizeInput(input: string): string {
  const div = document.createElement('div');
  div.textContent = input;
  return div.innerHTML;
}

/**
 * Validates that a value is not empty or whitespace
 *
 * @param value - Value to check
 * @returns true if value is non-empty
 */
export function isNotEmpty(value: string | null | undefined): boolean {
  return typeof value === 'string' && value.trim().length > 0;
}

/**
 * Validates workflow ID format (UUID v4)
 *
 * @param id - ID to validate
 * @returns true if ID matches UUID v4 format
 */
export function isValidUUID(id: string): boolean {
  const uuidRegex =
    /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
  return uuidRegex.test(id);
}

/**
 * Type guard to check if value is a non-empty string
 *
 * @param value - Value to check
 * @returns true if value is a non-empty string
 */
export function isNonEmptyString(
  value: unknown
): value is string {
  return typeof value === 'string' && value.length > 0;
}

/**
 * Type guard to check if value is defined
 *
 * @param value - Value to check
 * @returns true if value is not null or undefined
 */
export function isDefined<T>(value: T | null | undefined): value is T {
  return value !== null && value !== undefined;
}
