/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        'loco-primary': '#2563eb',
        'loco-secondary': '#7c3aed',
        'loco-success': '#10b981',
        'loco-warning': '#f59e0b',
        'loco-error': '#ef4444',
      },
    },
  },
  plugins: [],
}
