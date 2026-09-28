import react from '@vitejs/plugin-react';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';

const page = (file: string) => fileURLToPath(new URL(file, import.meta.url));

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  // Relative paths, so the site works at language-autocorrect.world (or any other address).
  base: './',
  build: {
    rolldownOptions: {
      // The download page, the release notes at /release-notes/ (also what the app shows after an update), the
      // privacy policy and credits at /privacy/ (the app links there too), and the download count at /downloads/
      // (not linked from anywhere; it's for the owner).
      input: {
        main: page('index.html'),
        releaseNotes: page('release-notes/index.html'),
        privacy: page('privacy/index.html'),
        downloads: page('downloads/index.html'),
      },
    },
  },
});
