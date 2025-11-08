/**
 * Tag Editor Component
 *
 * Provides an inline tag editor with autocomplete suggestions for workflow categorization.
 */

import { useState, useRef, useEffect } from 'react';
import { X, Plus, Tag } from 'lucide-react';

// ============================================================================
// Types
// ============================================================================

interface TagEditorProps {
  tags: string[];
  onChange: (tags: string[]) => void;
  suggestions?: string[];
  maxTags?: number;
  placeholder?: string;
}

// ============================================================================
// Predefined Tag Suggestions
// ============================================================================

export const PREDEFINED_TAGS = [
  'automation',
  'data-processing',
  'integration',
  'api',
  'webhook',
  'scheduled',
  'notification',
  'email',
  'database',
  'analytics',
  'monitoring',
  'reporting',
  'transformation',
  'validation',
  'backup',
  'sync',
  'import',
  'export',
  'crm',
  'marketing',
];

// ============================================================================
// Tag Editor Component
// ============================================================================

export function TagEditor({
  tags,
  onChange,
  suggestions = PREDEFINED_TAGS,
  maxTags = 10,
  placeholder = 'Add tag...',
}: TagEditorProps) {
  const [inputValue, setInputValue] = useState('');
  const [isInputVisible, setIsInputVisible] = useState(false);
  const [filteredSuggestions, setFilteredSuggestions] = useState<string[]>([]);
  const [selectedSuggestionIndex, setSelectedSuggestionIndex] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);

  // Filter suggestions based on input
  useEffect(() => {
    if (inputValue.trim()) {
      const filtered = suggestions.filter(
        (suggestion) =>
          suggestion.toLowerCase().includes(inputValue.toLowerCase()) &&
          !tags.includes(suggestion)
      );
      setFilteredSuggestions(filtered);
      setSelectedSuggestionIndex(0);
    } else {
      setFilteredSuggestions([]);
    }
  }, [inputValue, suggestions, tags]);

  // Focus input when visible
  useEffect(() => {
    if (isInputVisible && inputRef.current) {
      inputRef.current.focus();
    }
  }, [isInputVisible]);

  const handleAddTag = (tag: string) => {
    const trimmedTag = tag.trim().toLowerCase();

    if (!trimmedTag) return;
    if (tags.includes(trimmedTag)) return;
    if (tags.length >= maxTags) return;

    onChange([...tags, trimmedTag]);
    setInputValue('');
    setIsInputVisible(false);
  };

  const handleRemoveTag = (tagToRemove: string) => {
    onChange(tags.filter((tag) => tag !== tagToRemove));
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      e.preventDefault();

      if (filteredSuggestions.length > 0) {
        handleAddTag(filteredSuggestions[selectedSuggestionIndex]);
      } else {
        handleAddTag(inputValue);
      }
    } else if (e.key === 'Escape') {
      setInputValue('');
      setIsInputVisible(false);
    } else if (e.key === 'ArrowDown') {
      e.preventDefault();
      setSelectedSuggestionIndex((prev) =>
        Math.min(prev + 1, filteredSuggestions.length - 1)
      );
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setSelectedSuggestionIndex((prev) => Math.max(prev - 1, 0));
    } else if (e.key === 'Backspace' && !inputValue && tags.length > 0) {
      handleRemoveTag(tags[tags.length - 1]);
    }
  };

  return (
    <div className="relative">
      <div className="flex items-center gap-2 flex-wrap">
        {/* Tag Display */}
        {tags.map((tag) => (
          <div
            key={tag}
            className="inline-flex items-center gap-1 px-2 py-1 bg-loco-primary/10 text-loco-primary rounded text-xs font-medium"
          >
            <Tag className="w-3 h-3" />
            <span>{tag}</span>
            <button
              onClick={() => handleRemoveTag(tag)}
              className="hover:bg-loco-primary/20 rounded-full p-0.5 transition-colors"
              title="Remove tag"
            >
              <X className="w-3 h-3" />
            </button>
          </div>
        ))}

        {/* Input or Add Button */}
        {isInputVisible ? (
          <div className="relative">
            <input
              ref={inputRef}
              type="text"
              value={inputValue}
              onChange={(e) => setInputValue(e.target.value)}
              onKeyDown={handleKeyDown}
              onBlur={() => {
                setTimeout(() => {
                  setInputValue('');
                  setIsInputVisible(false);
                }, 200);
              }}
              placeholder={placeholder}
              className="px-2 py-1 border border-gray-300 rounded text-xs focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent"
            />

            {/* Suggestions Dropdown */}
            {filteredSuggestions.length > 0 && (
              <div className="absolute top-full left-0 mt-1 w-48 bg-white border border-gray-200 rounded-lg shadow-lg z-50 max-h-40 overflow-y-auto">
                {filteredSuggestions.map((suggestion, index) => (
                  <button
                    key={suggestion}
                    onClick={() => handleAddTag(suggestion)}
                    className={`w-full text-left px-3 py-2 text-xs hover:bg-gray-50 transition-colors ${
                      index === selectedSuggestionIndex ? 'bg-gray-100' : ''
                    }`}
                  >
                    <div className="flex items-center gap-2">
                      <Tag className="w-3 h-3 text-gray-400" />
                      <span>{suggestion}</span>
                    </div>
                  </button>
                ))}
              </div>
            )}
          </div>
        ) : (
          tags.length < maxTags && (
            <button
              onClick={() => setIsInputVisible(true)}
              className="inline-flex items-center gap-1 px-2 py-1 border border-dashed border-gray-300 hover:border-loco-primary text-gray-500 hover:text-loco-primary rounded text-xs transition-colors"
              title="Add tag"
            >
              <Plus className="w-3 h-3" />
              <span>Add tag</span>
            </button>
          )
        )}
      </div>
    </div>
  );
}
