import { FiHome, FiGrid, FiSettings, FiMoon, FiSun } from 'react-icons/fi';

export default function Sidebar({ activeView, setActiveView, darkMode, toggleDarkMode }) {
  const menuItems = [
    { id: 'dashboard', label: 'Dashboard', icon: FiHome },
    { id: 'flows', label: 'Flows', icon: FiGrid },
    { id: 'settings', label: 'Settings', icon: FiSettings },
  ];

  return (
    <div className="w-64 bg-white dark:bg-gray-800 shadow-lg">
      <div className="p-6">
        <h1 className="text-2xl font-bold text-gray-800 dark:text-white">Loco</h1>
        <p className="text-sm text-gray-600 dark:text-gray-400">Automation Platform</p>
      </div>

      <nav className="mt-6">
        {menuItems.map((item) => {
          const Icon = item.icon;
          return (
            <button
              key={item.id}
              onClick={() => setActiveView(item.id)}
              className={`w-full flex items-center px-6 py-3 text-left hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors ${
                activeView === item.id
                  ? 'bg-blue-50 dark:bg-gray-700 text-blue-600 dark:text-blue-400 border-r-4 border-blue-600'
                  : 'text-gray-700 dark:text-gray-300'
              }`}
            >
              <Icon className="mr-3" size={20} />
              <span>{item.label}</span>
            </button>
          );
        })}
      </nav>

      <div className="absolute bottom-0 w-64 p-6">
        <button
          onClick={toggleDarkMode}
          className="flex items-center justify-center w-full px-4 py-2 text-gray-700 dark:text-gray-300 bg-gray-100 dark:bg-gray-700 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600 transition-colors"
        >
          {darkMode ? <FiSun className="mr-2" /> : <FiMoon className="mr-2" />}
          {darkMode ? 'Light Mode' : 'Dark Mode'}
        </button>
      </div>
    </div>
  );
}
