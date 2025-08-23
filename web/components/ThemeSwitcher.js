import { useTheme } from 'next-themes';
import { useState, useEffect } from 'react';
import { SunIcon, MoonIcon, ComputerDesktopIcon } from '@heroicons/react/24/outline';

const ThemeSwitcher = () => {
  const [mounted, setMounted] = useState(false);
  const { theme, setTheme } = useTheme();

  useEffect(() => {
    setMounted(true);
  }, []);

  if (!mounted) {
    return null;
  }

  return (
    <div className="flex items-center space-x-2">
      <button aria-label="Switch to light theme" onClick={() => setTheme('light')} className={`p-2 rounded-md ${theme === 'light' ? 'bg-gray-200' : ''}`}>
        <SunIcon className="h-6 w-6" />
      </button>
      <button aria-label="Switch to dark theme" onClick={() => setTheme('dark')} className={`p-2 rounded-md ${theme === 'dark' ? 'bg-gray-700' : ''}`}>
        <MoonIcon className="h-6 w-6" />
      </button>
      <button aria-label="Use system theme" onClick={() => setTheme('system')} className={`p-2 rounded-md ${theme === 'system' ? 'bg-gray-200 dark:bg-gray-700' : ''}`}>
        <ComputerDesktopIcon className="h-6 w-6" />
      </button>
    </div>
  );
};

export default ThemeSwitcher;
