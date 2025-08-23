import { useState, useEffect } from 'react';
import Head from 'next/head';
import dynamic from 'next/dynamic';
import { Toaster } from 'react-hot-toast';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import Sidebar from '../components/Sidebar';
import Dashboard from '../components/Dashboard';
import FlowBuilder from '../components/FlowBuilder';
import Settings from '../components/Settings';

const queryClient = new QueryClient();

export default function Home() {
  const [activeView, setActiveView] = useState('dashboard');
  const [darkMode, setDarkMode] = useState(false);

  useEffect(() => {
    const savedTheme = localStorage.getItem('theme');
    if (savedTheme === 'dark') {
      setDarkMode(true);
      document.documentElement.classList.add('dark');
    }
  }, []);

  const toggleDarkMode = () => {
    setDarkMode(!darkMode);
    document.documentElement.classList.toggle('dark');
    localStorage.setItem('theme', !darkMode ? 'dark' : 'light');
  };

  const renderView = () => {
    switch (activeView) {
      case 'dashboard':
        return <Dashboard />;
      case 'flows':
        return <FlowBuilder />;
      case 'settings':
        return <Settings />;
      default:
        return <Dashboard />;
    }
  };

  return (
    <QueryClientProvider client={queryClient}>
      <div className={`min-h-screen ${darkMode ? 'dark' : ''}`}>
        <Head>
          <title>Loco - Automation Platform</title>
          <meta name="description" content="Lightweight automation platform" />
          <link rel="icon" href="/favicon.ico" />
        </Head>

        <div className="flex h-screen bg-gray-50 dark:bg-gray-900">
          <Sidebar 
            activeView={activeView} 
            setActiveView={setActiveView}
            darkMode={darkMode}
            toggleDarkMode={toggleDarkMode}
          />
          
          <main className="flex-1 overflow-y-auto">
            <div className="container mx-auto px-6 py-8">
              {renderView()}
            </div>
          </main>
        </div>

        <Toaster position="bottom-right" />
      </div>
    </QueryClientProvider>
  );
}
