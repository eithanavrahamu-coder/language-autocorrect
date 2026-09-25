import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  // Relative paths, so the site works at eithanavrahamu-coder.github.io/language-auto/ (or any other address).
  base: './',
});
